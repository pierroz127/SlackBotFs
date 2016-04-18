namespace SlackBotFs

open Newtonsoft.Json
open FSharp.Data


module Models =
    type Profile = { first_name: string option; last_name: string option; real_name: string option; email: string option }

    type User = { id: string; name: string; profile: Profile }

    type Channel = { id: string; name: string; creator: string; members: string list } 

    type Event(``type``: string) =
        [<JsonProperty(PropertyName = "type")>]
        member this.Type = ``type``

    type Ping(id: int) = 
        inherit Event("ping")

        [<JsonProperty(PropertyName = "id")>]
        member this.Id = id

        override this.ToString() = JsonConvert.SerializeObject(this)
        
    type Message(id: int, text: string, channel: Channel) =
        inherit Event("message")

        [<JsonProperty(PropertyName = "id")>]
        member this.Id = id
        
        [<JsonProperty(PropertyName = "text")>]
        member this.Text = text
       
        [<JsonProperty(PropertyName = "channel")>]
        member this.ChannelId = channel.id

        member this.WithId newId = new Message(newId, text, channel) 
       
        override this.ToString() = JsonConvert.SerializeObject(this)