namespace Trains

open System.Runtime.CompilerServices
open Trains.LiveDepartures

[<Extension>]
type LiveDeparturesExtensions() =

    [<Extension>]
    static member GetDepartures(departuresAndArrivalsTable:DeparturesAndArrivalsTable) = 
        match Stations.Country with
        | UK -> UK.getDepartures departuresAndArrivalsTable
        | Ireland -> IE.getDepartures departuresAndArrivalsTable

    [<Extension>]
    static member GetArrivals(departuresAndArrivalsTable:DeparturesAndArrivalsTable) = 
        match Stations.Country with
        | UK -> UK.getArrivals departuresAndArrivalsTable
        | Ireland -> IE.getArrivals departuresAndArrivalsTable
