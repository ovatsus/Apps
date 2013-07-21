namespace FSharp.Control

open System
open System.Net

type ILazyBlockUI = 

    abstract GlobalProgressMessage : string with get, set
    abstract LocalProgressMessage : string with set

    abstract HasItems : bool
    abstract SetItems : 'a[] -> unit

    abstract LastUpdated : string with set

    abstract StartTimer : Action -> unit
    abstract StopTimer : unit -> unit

    abstract OnException : string * exn -> unit

type LazyBlock<'a>(subject, emptyMessage, lazyAsync : LazyAsync<'a[]>, ui : ILazyBlockUI, useRefreshTimer, afterRefresh:Action, filter:Func<_,_>) as this=

    let cts = ref None

    let load() = 

        if useRefreshTimer then 
            ui.StopTimer()

        let refreshing = ui.HasItems

        let prefix = if refreshing then "Refreshing " else "Loading "

        if not refreshing then
            ui.LocalProgressMessage <- prefix + subject + "..."

        let first = ui.GlobalProgressMessage = ""

        let message = (if first || not (ui.GlobalProgressMessage.Contains prefix) then prefix else "") + subject + "...";
        
        ui.GlobalProgressMessage <- if first then message else ui.GlobalProgressMessage.Replace("...", " & " + message.ToLower())

        let removeGlobalProgressIndicator() =
            if ui.GlobalProgressMessage = message || ui.GlobalProgressMessage = prefix + message then
                ui.GlobalProgressMessage <- ""
            elif first then
                ui.GlobalProgressMessage <- 
                    let message = ui.GlobalProgressMessage.Replace(message.Replace("...", " & "), null)
                    if message.StartsWith prefix then message else prefix + message
            else
                ui.GlobalProgressMessage <- ui.GlobalProgressMessage.Replace(" & " + message.ToLower(), "...")

        let onSucess values =             
            ui.LocalProgressMessage <- if Array.length values = 0 then emptyMessage else ""
            removeGlobalProgressIndicator()
            ui.SetItems (if filter = null then values else filter.Invoke values)
            ui.LastUpdated <- "last updated at " + DateTime.Now.ToString("HH:mm:ss")
            cts := None
            if afterRefresh <> null then 
                afterRefresh.Invoke()
            if useRefreshTimer then
                ui.StartTimer (fun () -> this.Refresh())

        let onFailure (exn:exn) = 
            removeGlobalProgressIndicator()
            if not refreshing then
                ui.LocalProgressMessage <- 
                    if exn.Message.Length > 500 then
                        exn.Message.Substring(0, 500) + " ..."
                    else
                        exn.Message
            cts := None
            if (exn :? WebException) then
                if useRefreshTimer then
                    ui.StartTimer (fun () -> this.Refresh())
            else
                ui.OnException (prefix + subject, exn)

        let onCancel() = 
            removeGlobalProgressIndicator()
            ui.LocalProgressMessage <- ""
            cts := None
            
        cts := lazyAsync.GetValueAsync onSucess onFailure onCancel |> Some

    do load()

    member x.CanRefresh = (!cts).IsNone

    member x.Refresh() =
        if x.CanRefresh then
            lazyAsync.Reset()
            load()

    member x.Cancel() = 
        if useRefreshTimer then
            ui.StopTimer()
        !cts |> Option.iter (fun cts -> cts.Cancel())
        
