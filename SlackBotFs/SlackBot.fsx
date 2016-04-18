#r "../packages/FSharp.Data.2.2.5/lib/net40/FSharp.Data.dll"
#r "../packages/Newtonsoft.Json.8.0.3/lib/net45/Newtonsoft.Json.dll"
#r "System.Net"

open System
open System.IO
open System.Net.WebSockets
open System.Threading
open System.Threading.Tasks
open FSharp.Data

type RtmStart = JsonProvider<"rtmStartSample.json">

//type ReplyTo = ReplyTo of int
//type Message = Message of string
type Event = 
| ReplyTo of int
| Message of string*string
| Unknown

let response = Http.RequestString("https://slack.com/api/rtm.start", query=["token", "MY_TOKEN"], httpMethod="GET")

let rtmStart = RtmStart.Parse(response)

let channelId = 
    match rtmStart.Channels |> Array.tryFind (fun c -> c.Name = "testpierroz") with
    | Some(channel) -> channel.Id
    | _ -> ""

let mutable messageId = 0

let getMessageId() =
    messageId <- messageId + 1
    messageId

printfn "channel testpierroz id: %s" channelId

printfn "websocket url: %s" rtmStart.Url

let getUserName userId = 
    match rtmStart.Users |> Array.tryFind (fun u -> u.Id = userId) with
    | Some(user) -> Some(user.Name)
    | _ -> None

//printfn "users: "
//rtmStart.Users 
//|> Seq.toList 
//|> List.map(fun u -> printfn "%s" u.Name)
//

let awaitTask (t: Task) = t |> Async.AwaitIAsyncResult |> Async.Ignore

//let withDate s = sprintf "%s %s" (DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss.fff")) s

let debug = 
    let header = "[DEBUG]"
    printfn "%s %s" header

let logEvent = 
    let header = "[EVENT]"
    printfn "%s %s" header

let hasType typeName (properties: (string*JsonValue) []) = 
    match (properties |> Array.tryFind (fun (s, j) -> s = "type")) with
    | Some(s, j) -> j.AsString() = "message"
    | None -> false

let parseMessageText (properties: (string*JsonValue) []) = 
    match (properties |> Array.tryFind (fun (s, j) -> s = "text")) with
    | Some(s, j) -> j.AsString()
    | None -> ""

let parseMessageUser (properties: (string*JsonValue) []) = 
    match (properties |> Array.tryFind (fun (s, j) -> s = "user")) with
    | Some(s, j) -> j.AsString()
    | None -> ""

let parseMessage properties = 
    (parseMessageText properties), (parseMessageUser properties)

let parseEvent event = 
    let ev = JsonValue.Parse(event)
    match ev with
    | JsonValue.Record([| ("type", p); ("reply_to", t) |]) when p.AsString() = "pong" -> t.AsInteger() |> ReplyTo
    | JsonValue.Record(properties) when properties |> hasType "message" -> properties |> parseMessage |> Message
    | _ -> Unknown

let disconnect (webSocket: ClientWebSocket) =
    async {
        try
            do! webSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "", CancellationToken.None) |> awaitTask
        with
        | ex -> printfn "slack client shutting down but not gracefully"
    }



let sendData (webSocket: ClientWebSocket) (data: string) = 
    async {
        let buffer = new ArraySegment<byte>(System.Text.ASCIIEncoding.ASCII.GetBytes(data))
        do! webSocket.SendAsync(buffer, WebSocketMessageType.Text, true, CancellationToken.None) |> awaitTask
    }

let sendPings i (webSocket: ClientWebSocket) =
    async {
        do! Async.Sleep(10000)
        sprintf "PING %d" i |> debug
        let ping = sprintf @"{""id"": %d, ""type"":""ping""}" i
        do! sendData webSocket ping
    }

let sendMessage (webSocket: ClientWebSocket) (text: string) = 
    async {
        let message = sprintf @"{""id"": %d, ""type"": ""message"", ""channel"": ""%s"", ""text"": ""%s""}" (getMessageId()) channelId text
        do! sendData webSocket message
    }     

let processMessage (webSocket: ClientWebSocket) user =
    sprintf "coucou <@%s>" user |> sendMessage webSocket |> Async.Start

let processEvent (webSocket: ClientWebSocket) (event: Event) =
    match event with
    | ReplyTo(i) -> printfn "PONG!"; webSocket |> sendPings (i+1) |> Async.Start
    | Message(text, user) as m -> printfn "HEY WE GOT A MESSAGE: %s" text; processMessage webSocket user
    | _ -> ()

let rec receive (ms: System.IO.MemoryStream) (webSocket: ClientWebSocket) = 
    async {
        let buffer = new ArraySegment<byte>(Array.init 8192 (fun i -> byte(0)))
        let! res = webSocket.ReceiveAsync(buffer, CancellationToken.None) |> Async.AwaitTask
        debug "data received!"
        ms.Write(buffer.Array, buffer.Offset, res.Count)
        if res.EndOfMessage 
        then
            ms.Seek(0L, System.IO.SeekOrigin.Begin) |> ignore
            return ms.ToArray() 
        else
            return! webSocket |> receive ms
    }

let rec readMessages (webSocket: ClientWebSocket) =
    async {
        debug "trying to read a message"
        use ms = new MemoryStream()
        let! data = webSocket |> receive ms
        let message = System.Text.ASCIIEncoding.ASCII.GetString(data)
        logEvent message
        message |> parseEvent |> processEvent webSocket
        return! webSocket |> readMessages
    }

let connect url = 
    let webSocket = new ClientWebSocket()
    async {
        try
            do! webSocket.ConnectAsync(new Uri(rtmStart.Url), CancellationToken.None) |> awaitTask
            debug "Connected!"
            readMessages webSocket |> Async.Start
            do! sendPings 0 webSocket
        with
        | ex -> 
            printfn "%s %s" ex.Message ex.StackTrace
            do! webSocket |> disconnect
    }
    
connect rtmStart.Url |> Async.RunSynchronously


//let pong = @"{""type"":""pong"",""reply_to"":0}"

//let msg = @"{""type"": ""message"", ""channel"": ""C2147483705"", ""user"": ""U2147483697"", ""text"": ""Hello world"", ""ts"": ""1355517523.000005""}"

//open System
//
//let dateToString() = (DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss.fff"))
//
//let trace s = printfn "%s [TRACE] %s" (dateToString()) s
//
//#load "Models.fs"
//open SlackBotFs.Models
//
//let channel = {id="C1234"; name="testpierroz"; creator="U1234"; members=["U1234"; "U23456"]}
//let msg = Message(123, "coucou", channel)
//printfn "Message: %s" (msg.ToString())
