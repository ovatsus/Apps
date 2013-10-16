namespace Trains

open System.Runtime.CompilerServices
open Trains.LiveDepartures

[<Extension>]
type LiveDeparturesExtensions() =

    [<Extension>]
    static member GetDepartures(departuresTable:DeparturesTable, departureType:DepartureType) = 
        match Stations.Country with
        | UK -> UK.getDepartures departureType departuresTable
        | Ireland -> IE.getDepartures departureType departuresTable
