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

type Client(token: string, logger: ILogger) =
    let webSocket = new ClientWebSocket()
    let sendData (data: string) = 
        async {
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
        
    
    new(token) = Client(token, NLogger())

    member this.stop() =
         async {
            try
                do! webSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "", CancellationToken.None) |> Async.AwaitTask
            with
            | ex -> printfn "slack client shutting down but not gracefully"
        }
        
    member this.send (text: string) (channel: Channel) = 
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

            let rec readEvents() = 
                async {
                    logger.debug "trying to read a message"
                    use ms = new MemoryStream()
                    let! data = receive ms
                    let s = System.Text.ASCIIEncoding.ASCII.GetString(data)
                    s |> sprintf "event received: %s" |> logger.info
                    //s |> parseEvent |> emitEvent 
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
        
        connect() |> Async.RunSynchronously


            