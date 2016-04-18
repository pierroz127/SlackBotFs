namespace SlackBotFs

open System
open NLog

module Log =
    type ILogger = 
        abstract member trace: string -> unit
        abstract member debug : string -> unit
        abstract member info : string -> unit
        abstract member warn : string -> unit
        abstract member error : string -> unit

    type NLogger() =
        let _logger = LogManager.GetCurrentClassLogger()
        interface ILogger with  
            member this.trace s = _logger.Trace(s)
            member this.debug s = _logger.Debug(s)
            member this.info s = _logger.Info(s)
            member this.warn s = _logger.Warn(s)
            member this.error s = _logger.Error(s)

    type PrettyLogger() =
        let now() = (DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss.fff"))
        let log = printfn "%s %s %s" (now()) 
            
        interface ILogger with  
            member this.trace s = log "[TRACE]" s
            member this.debug s = log "[DEBUG]" s
            member this.info s = log "[INFO]" s
            member this.warn s = log "[WARN]" s
            member this.error s = log "[ERROR]" s
