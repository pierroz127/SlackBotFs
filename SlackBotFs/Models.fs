namespace SlackBotFs

open Newtonsoft.Json
open FSharp.Data


module Models =
    type Profile = { first_name: string option; last_name: string option; real_name: string option; email: string option }

    type User = { id: string; name: string; profile: Profile }

    type Channel = { id: string; name: string; creator: string; members: string list } 

    type Event(``type``: string) =
        let mutable _type = ``type``

        [<JsonProperty(PropertyName = "type")>]
        member this.Type
            with get() = _type
            and set(t: string) = _type <- t

    type Ping(id: int) = 
        inherit Event("ping")

        [<JsonProperty(PropertyName = "id")>]
        member this.Id = id

        override this.ToString() = JsonConvert.SerializeObject(this)

    type Pong() = 
        inherit Event("pong")
        let mutable _replyTo = 0

        [<JsonProperty(PropertyName = "reply_to")>]
        member this.ReplyTo 
            with get() = _replyTo
            and set(r: int) = _replyTo <- r

        static member parse (s: string) = JsonConvert.DeserializeObject<Pong>(s)
        
    type Message(id: int, text: string, channel: string) =
        inherit Event("message")
        let mutable _id = id
        let mutable _text = text
        let mutable _channel = channel
        let mutable _userId = ""

        [<JsonProperty(PropertyName = "id")>]
        member this.Id 
            with get() = _id
            and set(i: int) = _id <- i
        
        [<JsonProperty(PropertyName = "text")>]
        member this.Text
            with get() = _text
            and set(t: string) = _text <- t
       
        [<JsonProperty(PropertyName = "channel")>]
        member this.ChannelId 
            with get() = _channel
            and set(c: string) = _channel <- c

        [<JsonProperty(PropertyName = "user")>]
        member this.UserId
            with get() = _userId
            and set(u: string) = _userId <- u

        member this.WithId newId = new Message(newId, text, channel) 

        static member parse (s: string) = JsonConvert.DeserializeObject<Message>(s)
       
        override this.ToString() = JsonConvert.SerializeObject(this)