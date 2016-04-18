#r "../packages/FSharp.Data.2.2.5/lib/net40/FSharp.Data.dll"
#r "../packages/Newtonsoft.Json.8.0.3/lib/net45/Newtonsoft.Json.dll"
#r "../packages/NLog.4.3.0/lib/net45/NLog.dll"
#r "System.Net"

#load "Log.fs"
#load "Models.fs"
#load "Client.fs"

open System.Text.RegularExpressions
open SlackBotFs.Models
open SlackBotFs.Log

let (|Regex|_|) (pattern, input) = 
    match Regex.Match(input, pattern, RegexOptions.IgnoreCase) with 
    | m when m.Success -> Some(Regex m.Groups)
    | _ -> None

let (|Hello|_|) s = 
    match "^(hello|salut|bonjour|bonsoir)", s with
    | Regex g -> Some Hello
    | _ -> None

let (|WhatTimeIsIt|_|) s =
    match "^quelle heure est(-| )il", s with
    | Regex g -> Some WhatTimeIsIt
    | _ -> None

let getChannel (slack: SlackBotFs.Client) = 
    (slack.channels |> Array.find (fun c -> c.name = "testpierroz")).id

let answerHello (slack: SlackBotFs.Client) userId =
    slack.send (sprintf "Bonjour aussi <@%s>" userId) (getChannel slack)

let answerTime (slack: SlackBotFs.Client) userId =
    slack.send (sprintf "Il est %s <@%s>" (System.DateTime.Now.ToString("HH:mm:ss")) userId) (getChannel slack)

let processMessage (slack: SlackBotFs.Client) (m: Message) = 
    match m.Text with
    | Hello -> answerHello slack m.UserId
    | WhatTimeIsIt -> answerTime slack m.UserId
    | _ -> ()

let client = new SlackBotFs.Client("MY_TOKEN", PrettyLogger(), processMessage)
client.start() |> Async.RunSynchronously
