namespace Trains

open System
open System.ComponentModel
open System.Globalization
open System.Threading
open FSharp.Control
open FSharp.Data.Runtime

type DeparturesAndArrivalsTable = 
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
    member x.IsDelayed = 
        match x.Status with
        | Status.Delayed _ -> true
        | _ -> false
    member x.Expected = 
        match x.Status with
        | Status.Delayed mins -> Some (x.Due + Time.Create(mins))
        | _ -> None
    interface INotifyPropertyChanged with
        [<CLIEvent>]
        member x.PropertyChanged = x.PropertyChangedEvent

and Arrival = {
    Due : Time
    Origin : string
    Status : Status
    Platform : string option
    Details : LazyAsync<JourneyElement[]>
} with 
    override x.ToString() = sprintf "%A" x
    member x.PlatformIsKnown = x.Platform.IsSome
    member x.Expected = 
        match x.Status with
        | Status.Delayed mins -> Some (x.Due + Time.Create(mins))
        | _ -> None

and ArrivalInformation = {
    Due : Time option
    Destination : string
    Status : Status
} with 
    override x.ToString() = sprintf "%A" x
    member x.Expected = 
        match x.Status, x.Due with
        | Status.Delayed mins, Some due -> Some (due + Time.Create(mins))
        | _ -> x.Due

and Time = 
    { TotalMinutes : int }
    member x.Hours = x.TotalMinutes / 60
    member x.Minutes = x.TotalMinutes % 60
    override x.ToString() = sprintf "%02d:%02d" x.Hours x.Minutes
    static member Create(minutes) = 
        { TotalMinutes = minutes % 1440 }
    static member Create(hours, minutes) = 
        assert (hours >= 0 && hours <= 23)
        assert (minutes >= 0 && minutes <= 59)
        Time.Create(minutes + hours * 60)
    static member (+) (t1, t2) = 
        Time.Create(t1.TotalMinutes + t2.TotalMinutes)
    static member (-) (t1, t2) = 
        assert  (t1.TotalMinutes >= t2.TotalMinutes)
        Time.Create(t1.TotalMinutes - t2.TotalMinutes)
    static member Create(dt:DateTime) = 
        Time.Create(int dt.TimeOfDay.TotalMinutes)
    static member TryParse(str:string) = 
        let pos = str.IndexOf ':'
        if pos >= 0 then
            let hours = str.Substring(0, pos) |> TextConversions.AsInteger CultureInfo.InvariantCulture
            let minutes = str.Substring(pos+1) |> TextConversions.AsInteger CultureInfo.InvariantCulture
            match hours, minutes with
            | Some hours, Some minutes -> Some <| Time.Create(hours, minutes)
            | _ -> None
        else None
    static member Parse str = 
        match Time.TryParse str with
        | Some time -> time
        | None -> raise <| ParseError(sprintf "Invalid time:\n%s" str, null)
    member x.IsAfter(other:Time) = 
        x.TotalMinutes > other.TotalMinutes && (not (other.IsAfter x)) ||
        (x + Time.Create(4, 0)).TotalMinutes > (other + Time.Create(4, 0)).TotalMinutes

and Status =
    | OnTime
    | Delayed of mins:int
    | DelayedIndefinitely
    | Cancelled
    | NoReport
    override x.ToString() =
        match x with
        | OnTime -> "On time"
        | Delayed mins -> sprintf "Delayed %d mins" mins
        | DelayedIndefinitely -> sprintf "Delayed"
        | Cancelled -> "Cancelled"
        | NoReport -> "No Report"

and JourneyElement = {
    Arrives : Time option
    Station : string
    Status : JourneyElementStatus
    Platform : string option
    IsAlternateRoute : bool
} with
    override x.ToString() = sprintf "%A" x
    member x.PlatformIsKnown = x.Platform.IsSome
    member x.Expected = 
        match x.Status, x.Arrives with
        | Delayed (mins = mins), Some arrives -> Some (arrives + Time.Create(mins))
        | _ -> None

and JourneyElementStatus =
    | OnTime of departed:bool
    | NoReport
    | Cancelled
    | Delayed of departed:bool * mins:int
    | DelayedIndefinitely
    override x.ToString() =
        match x with
        | OnTime _ -> "On time"
        | NoReport -> "No report"
        | Cancelled -> "Cancelled"
        | Delayed (mins = mins) -> sprintf "Delayed %d mins" mins
        | DelayedIndefinitely -> sprintf "Delayed"
    member x.HasDeparted =
        match x with
        | OnTime hasDeparted -> hasDeparted
        | NoReport -> true
        | Cancelled -> false
        | Delayed (departed = departed) -> departed
        | DelayedIndefinitely -> false

and ParseError(msg:string, exn) = 
    inherit Exception(msg, exn)

type DeparturesAndArrivalsTable with
    
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
            DeparturesAndArrivalsTable.Create(station)
        else
            { Station = station 
              CallingAt = callingAt |> Some}
    
    static member Parse (str:string) =
        let pos = str.IndexOf '|'
        if pos >= 0 then
            let stationFound, station = str.Substring(0, pos) |> Stations.tryGet
            let callingAtFound, callingAt = str.Substring(pos + 1) |> Stations.tryGet
            if stationFound && callingAtFound then
                DeparturesAndArrivalsTable.Create(station, callingAt)
            else
                Unchecked.defaultof<_>
        else
            let stationFound, station = str |> Stations.tryGet
            if stationFound then
                DeparturesAndArrivalsTable.Create(station)
            else
                Unchecked.defaultof<_>

type Departure with
    
    member departure.SubscribeToDepartureInformation callingAtFilter (propertyChangedEvent:Event<_,_>) (synchronizationContext:SynchronizationContext) token = 
    
        let postArrivalInformation (journeyElements:JourneyElement[]) index = 
        
            let journeyElement = journeyElements.[index]

            let destination = 
                if index = journeyElements.Length - 1 then "" // don't display it as it would be repeated
                else journeyElement.Station // display the smaller (journeyElement.Station.Length <= calligAt.Name.Length)

            let status =
                match journeyElement.Status with
                | Delayed (_, mins) -> Status.Delayed mins
                | Cancelled -> Status.Cancelled
                | _ -> Status.OnTime

            departure.Arrival := 
                Some { ArrivalInformation.Due = journeyElement.Arrives
                       Destination = destination
                       Status = status }
        
            let triggerProperyChanged _ = 
                propertyChangedEvent.Trigger(departure, PropertyChangedEventArgs "Arrival")
                propertyChangedEvent.Trigger(departure, PropertyChangedEventArgs "ArrivalIsKnown")
        
            synchronizationContext.Post(SendOrPostCallback(triggerProperyChanged), null)

        let onJourneyElementsObtained (journeyElements:JourneyElement[]) =
      
            let isAfterDeparture journeyElement = 
                journeyElement.Arrives.IsNone || journeyElement.Arrives.Value.IsAfter departure.Due

            if journeyElements.Length <> 0 then

                let index = 
                    match callingAtFilter with
                    | Some callingAtFilter -> 
                        match journeyElements
                              |> Array.tryFindIndex (fun journeyElement -> 
                                journeyElement.Station = callingAtFilter
                                && isAfterDeparture journeyElement) with
                        | Some index -> Some index
                        | None -> journeyElements 
                                  |> Array.tryFindIndex (fun journeyElement -> 
                                    // Sometimes there's no 100% match, eg: Farringdon vs Farringdon (London)
                                    (callingAtFilter.StartsWith journeyElement.Station || journeyElement.Station.StartsWith callingAtFilter)
                                    && isAfterDeparture journeyElement)
                    | None -> Some <| journeyElements.Length - 1
                
                index |> Option.iter (postArrivalInformation journeyElements)
      
        if synchronizationContext <> null then //it's null on sample data

            match departure.Status with 
            | Status.Cancelled | Status.DelayedIndefinitely | Status.NoReport -> ()
            | _ -> departure.Details.GetValueAsync (Some token) onJourneyElementsObtained ignore ignore |> ignore
