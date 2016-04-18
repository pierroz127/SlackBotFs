namespace SlackBotFs

open System
open System.IO
open System.Net.WebSockets
open System.Text
open System.Threading
open System.Threading.Tasks
open FSharp.Data
open SlackBotFs.Log
open SlackBotFs.Models

type RtmStart = JsonProvider<"rtmStartSample.json">

type Client(token: string, logger: ILogger, processMessage: Client -> Message -> unit) =
    let webSocket = new ClientWebSocket()
    let sendData (data: string) = 
        async {
            sprintf "Send Data %s" data |> logger.debug
            let buffer = new ArraySegment<byte>(ASCIIEncoding.ASCII.GetBytes(data))
            do! webSocket.SendAsync(buffer, WebSocketMessageType.Text, true, CancellationToken.None) |> Async.AwaitTask
        }
                
    let senderAgent = MailboxProcessor.Start(fun inbox -> 
        let rec senderLoop id = 
            async {
                let! o = inbox.Receive() 
                let msg = o |> unbox<Message>
                do! msg.WithId(id).ToString() |> sendData
                return! senderLoop (id+1)
            }
        senderLoop 1)

    let sendPing id = 
        async {
            let ping = new Ping(id)
            do! ping.ToString() |> sendData
        }

    let rtm = Http.RequestString("https://slack.com/api/rtm.start", query=["token", token], httpMethod="GET") |> RtmStart.Parse
    
    let user (u: RtmStart.User) =
        { id = u.Id
          name = u.Name
          profile = 
            { first_name = u.Profile.FirstName
              last_name =  u.Profile.LastName
              real_name = u.Profile.RealName
              email = u.Profile.Email }}

    let channel (c: RtmStart.Channel) =
        { id = c.Id
          name = c.Name 
          creator = c.Creator
          members = c.Members |> Array.toList }

    let _users = rtm.Users |> Array.map(fun u-> u |> user)    
    let _channels = rtm.Channels |> Array.map(fun c -> c |> channel)    
            
    new(token, processMessage) = Client(token, NLogger(), processMessage)

    member this.users = _users

    member this.channels = _channels

    member this.stop() =
         async {
            try
                do! webSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "", CancellationToken.None) |> Async.AwaitTask
            with
            | ex -> printfn "slack client shutting down but not gracefully"
        }
        
    member this.send (text: string) (channel: string) = 
        senderAgent.Post(Message(0, text, channel))

    member this.start() =         
        let response = Http.RequestString("https://slack.com/api/rtm.start", query=["token", token], httpMethod="GET")
        let rtmStart = RtmStart.Parse(response)
        let connect () = 
            let rec receive (ms: System.IO.MemoryStream) = 
                async {
                    let buffer = new ArraySegment<byte>(Array.init 8192 (fun i -> byte(0)))
                    let! res = webSocket.ReceiveAsync(buffer, CancellationToken.None) |> Async.AwaitTask
                    logger.debug "data received!"
                    ms.Write(buffer.Array, buffer.Offset, res.Count)
                    if res.EndOfMessage 
                    then
                        ms.Seek(0L, System.IO.SeekOrigin.Begin) |> ignore
                        return ms.ToArray() 
                    else
                        return! receive ms
                }

            let hasType typeValue (properties: (string*JsonValue) []) = 
                match (properties |> Array.tryFind (fun (s, j) -> s = "type")) with
                | Some(s, j) -> j.AsString() = typeValue
                | None -> false

            let (|IsMessage|_|) j = 
                match j with 
                | JsonValue.Record(properties) when properties |> hasType "message" -> Some IsMessage
                | _ -> None

            let (|IsPong|_|) j = 
                match j with 
                | JsonValue.Record(properties) when properties |> hasType "pong" -> Some IsPong
                | _ -> None

            let processPong (pong: Pong) = 
                async {
                    do! Async.Sleep(5000)
                    do! sendPing (pong.ReplyTo + 1)
                }
                |> Async.Start
                Async.Sleep(1)

            let processEvent (s: string) = 
                match JsonValue.Parse(s) with
                | IsMessage -> s |> Message.parse |> processMessage this; Async.Sleep(1000)
                | IsPong -> s |> Pong.parse |> processPong 
                | _ -> Async.Sleep(1000)

            let rec readEvents() = 
                async {
                    logger.debug "trying to read a message"
                    use ms = new MemoryStream()
                    let! data = receive ms
                    let s = System.Text.ASCIIEncoding.ASCII.GetString(data)
                    s |> sprintf "event received: %s" |> logger.info
                    do! s |> processEvent 
                    return! readEvents()
                }

            async {
                try
                    do! webSocket.ConnectAsync(new Uri(rtmStart.Url), CancellationToken.None) |> Async.AwaitTask
                    logger.debug "Connected!"
                    readEvents() |> Async.Start
                    do! sendPing 0 
                with
                | ex -> 
                    logger.error (sprintf "%s %s" ex.Message ex.StackTrace)
                    this.stop() |> ignore
            }
        
        connect() 


            