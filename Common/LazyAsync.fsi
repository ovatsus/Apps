namespace FSharp.Control

open System
open System.Threading

[<Class>]
/// Allows to expose a F# async value in a C#-friendly API with the
/// semantics of Lazy<> (compute on demand and guarantee only one computation)
type LazyAsync<'a> =

    /// Will start calculation if not started.
    /// The callbacks will be executed on the same thread that called GetValueAsync
    member GetValueAsync : parentCancellationToken:(CancellationToken option) -> onSuccess:('a -> unit) -> onFailure:(exn -> unit) -> onCancel:(unit -> unit) -> CancellationTokenSource

    member Reset : unit -> unit

    member ResetIfFailed : unit -> unit

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module LazyAsync =
    
    val fromAsync : Async<'a> -> LazyAsync<'a>
    
    val fromValue : 'a -> LazyAsync<'a>
    
    val toAsync : LazyAsync<'a> -> Async<'a>

    /// Will not start calculation if not started
    val map : ('a -> 'b) -> LazyAsync<'a> -> LazyAsync<'b>
    
    /// Will not start calculation if not started
    val subscribe : onSuccess:('a -> unit) -> onFailure:(exn -> unit) -> LazyAsync<'a> -> LazyAsync<'a>
