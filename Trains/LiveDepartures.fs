namespace Trains

open System
open System.ComponentModel
open FSharp.Control

type DeparturesTable = 
    { Station : Station
      CallingAt : Station option }
    override x.ToString() =
        match x.CallingAt with
        | None -> x.Station.Name
        | Some callingAt -> sprintf "%s calling at %s" x.Station.Name callingAt.Name
    member x.ToSmallString() =
        match x.CallingAt with
        | None -> x.Station.Code
        | Some callingAt -> sprintf "%s calling at %s" x.Station.Code callingAt.Code
    member x.HasDestinationFilter = x.CallingAt.IsSome
    member x.WithoutFilter = 
        if not x.HasDestinationFilter then failwith "%A doesn't have a destination filter" x
        { Station = x.Station
          CallingAt = None }
    member x.Reversed =
        if not x.HasDestinationFilter then failwith "%A can't be reversed" x
        { Station = x.CallingAt.Value
          CallingAt = Some x.Station }

type Departure = {
    Due : Time
    Destination : string
    DestinationDetail : string
    Status : Status
    Platform : string option
    Details : LazyAsync<JourneyElement[]>
    Arrival : ArrivalInformation option ref
    PropertyChangedEvent : IEvent<PropertyChangedEventHandler, PropertyChangedEventArgs>
} with 
    override x.ToString() = sprintf "%A" x
    member x.PlatformIsKnown = x.Platform.IsSome
    member x.ArrivalIsKnown = x.Arrival.Value.IsSome
    member x.Expected = 
        match x.Status with
        | Status.Delayed mins -> Some (x.Due + Time.Create(mins))
        | _ -> None
    interface INotifyPropertyChanged with
        [<CLIEvent>]
        member x.PropertyChanged = x.PropertyChangedEvent

and ArrivalInformation = {
    Due : Time
    Destination : string
    Status : Status
} with 
    override x.ToString() = sprintf "%A" x
    member x.Expected = 
        match x.Status with
        | Status.Delayed mins -> x.Due + Time.Create(mins)
        | _ -> x.Due

and Time = 
    private { TotalMinutes : int }
    member x.Hours = x.TotalMinutes / 60
    member x.Minutes = x.TotalMinutes % 1440 % 60
    override x.ToString() = sprintf "%02d:%02d" x.Hours x.Minutes
    static member Create(hours, minutes) = 
        assert (hours >= 0 && hours <= 23)
        assert (minutes >= 0 && minutes <= 59)
        { TotalMinutes = (minutes + hours * 60) % 1440 }
    static member Create(minutes) = 
        { TotalMinutes = minutes % 1440 }
    static member (+) (t1, t2) = 
        { TotalMinutes = t1.TotalMinutes + t2.TotalMinutes }
    static member Create(dt:DateTime) = 
        { TotalMinutes = dt.TimeOfDay.Minutes }

and Status =
    | OnTime
    | Delayed of int
    | Cancelled
    override x.ToString() =
        match x with
        | OnTime -> "On time"
        | Delayed mins -> sprintf "Delayed %d mins" mins
        | Cancelled -> "Cancelled"

and JourneyElement = {
    Departs : Time
    Station : string
    Status : JourneyElementStatus
    Platform : string option
    IsAlternateRoute : bool
} with
    override x.ToString() = sprintf "%A" x
    member x.PlatformIsKnown = x.Platform.IsSome
    member x.Expected = 
        match x.Status with
        | Delayed (_, mins) -> Some (x.Departs + Time.Create(mins))
        | _ -> None

and JourneyElementStatus =
    | OnTime of (*departed*)bool
    | NoReport
    | Cancelled
    | Delayed of (*departed*)bool * int
    override x.ToString() =
        match x with
        | OnTime _ -> "On time"
        | NoReport -> "No report"
        | Cancelled -> "Cancelled"
        | Delayed (_, mins) -> sprintf "Delayed %d mins" mins
    member x.HasDeparted =
        match x with
        | OnTime hasDeparted -> hasDeparted
        | NoReport -> true
        | Cancelled -> false
        | Delayed (hasDeparted, _) -> hasDeparted

type DepartureType = 
    | Departure
    | Arrival
    override x.ToString() = 
        match x with
        | Departure -> "dep"
        | Arrival -> "arr"

type ParseError(msg:string, exn) = 
    inherit Exception(msg, exn)

type DeparturesTable with
    
    member x.Serialize() =
        match x.CallingAt with
        | None -> x.Station.Code
        | Some callingAt -> x.Station.Code + "|" + callingAt.Code

    member x.Match(withoutDestinationFilter:Func<_,_>, withDestinationFilter:Func<_,_,_>) =
        match x.CallingAt with
        | None -> withoutDestinationFilter.Invoke(x.Station)
        | Some callingAt -> withDestinationFilter.Invoke(x.Station, callingAt)        

    static member Create(station) =
        { Station = station
          CallingAt = None}

    static member Create(station, callingAt) =
        if obj.ReferenceEquals(callingAt, null) then
            DeparturesTable.Create(station)
        else
            { Station = station 
              CallingAt = callingAt |> Some}
    
    static member Parse (str:string) =
        let pos = str.IndexOf '|'
        if pos >= 0 then
            let station = str.Substring(0, pos) |> Stations.get
            let callingAt = str.Substring(pos + 1) |> Stations.get
            DeparturesTable.Create(station, callingAt)
        else
            let station = str |> Stations.get
            DeparturesTable.Create(station)
