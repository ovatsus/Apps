namespace FSharp.Control

open System
open System.Threading

type ComputationResult<'a> = 
    | Success of 'a
    | Failure of exn

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module ComputationResult = 

    let fromChoice choice =
        match choice with
        | Choice1Of2 value -> Success value
        | Choice2Of2 exn -> Failure exn
    
    let map f = function
        | Success value -> 
            try
                Success (f value)
            with exn ->
                Failure exn
        | Failure exn -> Failure exn

    let combine onSuccess onFailure = function
        | Success result -> onSuccess result
        | Failure exn -> onFailure exn

type AsyncState<'a> =
    | NotStarted of Async<ComputationResult<'a>>
    | Started of Async<ComputationResult<'a>>
    | Completed of Async<ComputationResult<'a>> * ComputationResult<'a>

type LazyAsync<'a>(state:AsyncState<'a>) = 

    let state = ref state
    let mutable completed = Event<_>()
    let mutable icompleted = completed.Publish

    member __.DoWhenCompleted startIfNotRunning f cancelF =
        let cancelationTokenSource = new CancellationTokenSource()
        let startWithCancellationToken computation =
            let onCancel (_:obj) = 
                lock state <| fun () -> 
                    match !state with
                    | Started(asyncValue) -> state := NotStarted(asyncValue)
                    | _ -> ()
                cancelF()
            cancelationTokenSource.Token.Register(Action<obj>(onCancel), ()) |> ignore
            Async.Start(computation, cancelationTokenSource.Token)
        lock state <| fun () -> 
            match !state with
            | Completed(asyncValue, value) ->
                Some <| fun () -> f value
            | Started(asyncValue) ->
                icompleted.Add f
                None
            | NotStarted(asyncValue) ->
                icompleted.Add f
                if startIfNotRunning then
                    state := Started asyncValue
                    Some <| fun () ->
                        async {
                            let! value = asyncValue
                            lock state <| fun () -> state := Completed(asyncValue, value)
                            completed.Trigger value
                        } |> startWithCancellationToken
                else
                    None
        |> Option.iter (fun continuation -> continuation())
        cancelationTokenSource

    member x.GetValueAsync onSuccess onFailure onCancel = 

        let synchronizationContext = SynchronizationContext.Current

        let doInOriginalThread f arg = 
            if synchronizationContext <> null then
                synchronizationContext.Post ((fun _ -> f arg), null)
            else
                f arg

        let cancelF = doInOriginalThread (fun _ -> onCancel())

        ComputationResult.combine onSuccess onFailure
        |> doInOriginalThread
        |> x.DoWhenCompleted true <| cancelF

    member __.Reset() =
        lock state <| fun () -> 
            match !state with
            | Completed(asyncValue, value) ->
                state := NotStarted asyncValue
                completed <- Event<_>()
                icompleted <- completed.Publish
            | _ -> ()

    member __.ResetIfFailed() =
        lock state <| fun () -> 
            match !state with
            | Completed(asyncValue, value) ->
                match value with
                | Failure _ -> 
                    state := NotStarted asyncValue
                    completed <- Event<_>()
                    icompleted <- completed.Publish
                | _ -> ()
            | _ -> ()

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module LazyAsync =

    let fromAsync asyncValue = 
        let asyncValue = async {
            let! value = Async.Catch asyncValue
            return ComputationResult.fromChoice value
        }        
        LazyAsync(NotStarted asyncValue)

    let fromValue value =
        LazyAsync(Completed(async { return Success value }, Success value))

    let map f (x:LazyAsync<'a>) =
        let asyncValue = async { 
            let! value = Async.FromContinuations <| fun (cont, _, _) -> cont |> x.DoWhenCompleted true |> ignore
            return value |> ComputationResult.map f
        }
        LazyAsync(NotStarted asyncValue)

    let subscribe onSuccess onFailure (x:LazyAsync<'a>) =
        ComputationResult.combine onSuccess onFailure
        |> x.DoWhenCompleted false
        |> ignore
        x
