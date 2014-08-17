namespace FSharp.Control

open System
open System.Net

type LazyBlockUIState =
    | Loading of string
    | Refreshing of string
    static member Create s refreshing = 
        s |> if refreshing then Refreshing else Loading

type ILazyBlockUI<'a> = 

    abstract GlobalState : LazyBlockUIState list with get, set

    abstract SetGlobalProgressMessage : string -> unit
    abstract SetLocalProgressMessage : string -> unit

    abstract GetWebExceptionMessage : WebException -> string

    abstract HasItems : bool
    abstract SetItems : 'a -> unit

    abstract SetLastUpdated : string -> unit

    abstract StartTimer : Action -> unit
    abstract StopTimer : unit -> unit

    abstract OnException : string * exn -> unit

type LazyBlock<'a>(subject, emptyMessage, lazyAsync:LazyAsync<'a>, isEmpty:Func<_,_>, ui : ILazyBlockUI<'a>, useRefreshTimer,
                   beforeLoad:Action<bool>, afterLoad:Action<bool>, filter:Func<_,_>) as this =

    let cts = ref None

    let rec getMessage = function
        | [] -> ""
        | [Loading x] -> sprintf "Loading %s..." x
        | [Refreshing x] -> sprintf "Refreshing %s..." x
        | x when Seq.distinct x |> Seq.length = 1 -> (getMessage [x.Head]).Replace("...", "s...")
        | [Loading x; Loading y] -> sprintf "Loading %s & %s..." x y
        | [Refreshing x; Refreshing y] -> sprintf "Refreshing %s & %s..." x y
        | Loading x::xs -> sprintf "Loading %s & %s" x ((getMessage xs).ToLower())
        | Refreshing x::xs -> sprintf "Refreshing %s & %s"  x ((getMessage xs).ToLower())        

    let load isRefresh = 

        if beforeLoad <> null then 
            beforeLoad.Invoke isRefresh

        if useRefreshTimer then 
            ui.StopTimer()

        let refreshing = ui.HasItems

        let state = LazyBlockUIState.Create subject refreshing

        if not refreshing then
            ui.SetLocalProgressMessage (getMessage [state])

        ui.GlobalState <- ui.GlobalState @ [state]
        ui.SetGlobalProgressMessage (getMessage ui.GlobalState)

        let removeGlobalProgressIndicator() =
            ui.GlobalState <- ui.GlobalState |> List.filter ((<>) state)
            ui.SetGlobalProgressMessage (getMessage ui.GlobalState)        

        let onSucess values =             
            ui.SetLocalProgressMessage (if isEmpty.Invoke values then emptyMessage else "")
            removeGlobalProgressIndicator()
            ui.SetItems (if filter = null then values else filter.Invoke values)
            ui.SetLastUpdated ("last updated at " + DateTime.Now.ToString("HH:mm:ss"))
            if afterLoad <> null then 
                afterLoad.Invoke true
            if useRefreshTimer then
                ui.StartTimer (fun () -> this.Refresh())

        let onFailure (exn:exn) = 
            removeGlobalProgressIndicator()
            let isWebException = exn :? WebException
            if not refreshing then
                ui.SetLocalProgressMessage
                    (if isWebException then ui.GetWebExceptionMessage(exn :?> WebException)
                     elif exn.Message.Length > 500 then exn.Message.Substring(0, 500) + " ..."
                     else exn.Message)
            if afterLoad <> null then 
                afterLoad.Invoke false
            if isWebException then
                if useRefreshTimer then
                    ui.StartTimer (fun () -> this.Refresh())
            else
                ui.OnException (getMessage [state], exn)

        let onCancel() = 
            removeGlobalProgressIndicator()
            ui.SetLocalProgressMessage ""
            if afterLoad <> null then 
                afterLoad.Invoke false
            
        cts := lazyAsync.GetValueAsync None onSucess onFailure onCancel |> Some

    do lazyAsync.ResetIfFailed()
    do load false

    member x.Refresh() =
        x.Cancel()
        lazyAsync.Reset()
        load true

    member __.Cancel() = 
        if useRefreshTimer then
            ui.StopTimer()
        !cts |> Option.iter (fun cts -> cts.Cancel(); cts.Dispose())
        cts := None
        
