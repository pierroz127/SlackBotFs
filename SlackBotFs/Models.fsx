#r "../packages/FSharp.Data.2.2.5/lib/net40/FSharp.Data.dll"
#r "../packages/Newtonsoft.Json.8.0.3/lib/net45/Newtonsoft.Json.dll"
#load "Models.fs"

open SlackBotFs.Models

let channel = {id="C1234"; name="testpierroz"; creator="U1234"; members=["U1234"; "U23456"]}
let msg = Message(123, "coucou", "testpierroz")
printfn "Message: %s" (msg.ToString())

let pong = @"{""type"":""pong"", ""reply_to"": 123}" |> Pong.parse
