module FSharp.Control

open System

[<Class>]
/// Allows to expose a F# async value in a C#-friendly API with the
/// semantics of Lazy<> (compute on demand and guarantee only one computation)
type LazyAsync<'a> =

    /// Will start calculation if not started.
    /// callBack will be called on the same thread that called GetValueAsync
    member GetValueAsync : onSuccess:Action<'a> * onFailure:Action<exn> -> unit

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module LazyAsync =
    
    val fromAsync : Async<'a> -> LazyAsync<'a>
    
    val fromValue : 'a -> LazyAsync<'a>
    
    /// Will not start calculation if not started
    val map : ('a -> 'b) -> LazyAsync<'a> -> LazyAsync<'b>
    
    /// Will not start calculation if not started
    val subscribe : onSuccess:('a -> unit) -> onFailure:(exn -> unit) -> LazyAsync<'a> -> LazyAsync<'a>
