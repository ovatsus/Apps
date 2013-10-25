module Trains.LiveDepartures.IE

open System.Threading
open HtmlAgilityPack
open HtmlAgilityPack.FSharp
open FSharp.Control
open FSharp.Data
open FSharp.Net
open Trains

//http://api.irishrail.ie/realtime/index.htm
type private LocationType =
    | Origin
    | Stop
    | TimingPoint //non stopping location
    | Destination
    override x.ToString() =
        match x with
        | Origin -> "O"
        | Stop -> "S"
        | TimingPoint -> "T"
        | Destination -> "D"

type private StationDataXmlT = XmlProvider<"StationData.xml">
type private TrainMovementsStationDataXmlT = XmlProvider<"TrainMovements.xml">

let private xmlToJourneyElement (xml:TrainMovementsStationDataXmlT.DomainTypes.ObjTrainMovements) =
    let hasDeparted = xml.Departure.IsSome
    let delayedMins = int (if xml.LocationType = LocationType.Origin.ToString()
                           then xml.ExpectedDeparture - xml.ScheduledDeparture
                           else xml.ExpectedArrival - xml.ScheduledArrival).TotalMinutes
    { Arrives = Time.Create (if xml.LocationType = LocationType.Origin.ToString()
                             then xml.ScheduledDeparture
                             else xml.ScheduledArrival)
      Station = xml.LocationFullName
      Status = if delayedMins > 0 then Delayed(hasDeparted, delayedMins) else OnTime(hasDeparted)
      Platform = None
      IsAlternateRoute = false }

let private getJourneyDetails trainCode trainDate = async {

    let url = sprintf "http://api.irishrail.ie/realtime/realtime.asmx/getTrainMovementsXML?TrainId=%s&TrainDate=%s" trainCode trainDate
    let! xml = Http.AsyncRequestString url
    let xmlT = TrainMovementsStationDataXmlT.Parse xml

    let getDepartures() = 
        try 
            xmlT.GetObjTrainMovements()
            |> Seq.filter (fun xml -> xml.LocationType <> LocationType.TimingPoint.ToString())
            |> Seq.map xmlToJourneyElement
            |> Seq.toArray
        with 
        | exn -> raise <| ParseError(sprintf "Failed to parse departures xml from %s:\n%s" url xml, exn)

    return getDepartures() 
}

let private trim (s:string) = s.Trim()

let private xmlToDeparture callingAtFilter (xml:StationDataXmlT.DomainTypes.ObjStationData) =

    let arrivalInformation =
        match callingAtFilter with
        | None -> 
            Some { ArrivalInformation.Due = Time.Create xml.Destinationtime
                   Destination = ""
                   Status = Status.OnTime }
        | Some station when xml.Destination = station -> 
            Some { ArrivalInformation.Due = Time.Create xml.Destinationtime
                   Destination = ""
                   Status = Status.OnTime }
        | Some _ -> None // we'll need to fetch it from getJourneyDetails asynchronously
    
    let propertyChangedEvent = Event<_,_>()

    trim xml.Traincode, 
    ({ Due = Time.Create xml.Schdepart
       Destination = xml.Destination
       DestinationDetail = ""
       Status = if xml.Late > 0 then Status.Delayed xml.Late else Status.OnTime
       Platform = None
       Details = LazyAsync.fromAsync (getJourneyDetails (trim xml.Traincode) <| xml.Traindate.ToString("dd MMM yyyy"))
       Arrival = ref arrivalInformation
       PropertyChangedEvent = propertyChangedEvent.Publish }, callingAtFilter, propertyChangedEvent)

let private xmlToArrival callingAtFilter (xml:StationDataXmlT.DomainTypes.ObjStationData) =
    trim xml.Traincode,
    { Due = Time.Create xml.Scharrival
      Origin = xml.Origin
      Status = if xml.Late > 0 then Status.Delayed xml.Late else Status.OnTime
      Platform = None
      Details = LazyAsync.fromAsync (getJourneyDetails (trim xml.Traincode) <| xml.Traindate.ToString("dd MMM yyyy")) }

let private getCallingPoints (tr:HtmlNode) =
    let trainId = 
        tr 
        |> elements "td" 
        |> Seq.head 
        |> element "a" 
        |> innerText
        |> trim
    let callingPoints = 
        tr
        |> parent
        |> elements "tr"
        |> Seq.filter (hasId ("train" + trainId))
        |> Seq.collect (descendants "tr")
        |> Seq.filter (hasClass "")
        |> Seq.map (fun tr -> let cells = tr |> elements "td" |> Seq.toArray
                              let station = cells.[1].InnerText |> trim
                              station)
        |> Set.ofSeq
    trainId, callingPoints

let private getDeparturesOrArrivals forDepartures mapper getOutput (departuresAndArrivalsTable:DeparturesAndArrivalsTable) = 

    let xmlUrl = "http://api.irishrail.ie/realtime/realtime.asmx/getStationDataByCodeXML?StationCode=" + departuresAndArrivalsTable.Station.Code

    let getDeparturesOrArrivals callingAtFilter xml extraFilter =
        try 
            let xmlT = StationDataXmlT.Parse xml
            xmlT.GetObjStationDatas()
            |> Seq.filter (fun xml -> xml.Locationtype <> (if forDepartures then LocationType.Destination else LocationType.Origin).ToString())
            |> Seq.map (mapper callingAtFilter)
            |> extraFilter
            |> Seq.map getOutput
            |> Seq.toArray
        with 
        | exn -> raise <| ParseError(sprintf "Failed to parse xml from %s:\n%s" xmlUrl xml, exn)
    
    let htmlUrl = "http://www.irishrail.ie/realtime/station_details.jsp?ref=" + departuresAndArrivalsTable.Station.Code
    
    let getCallingPointsByTrain html =
        let (&&&) f1 f2 arg = f1 arg && f2 arg
        try 
            createDoc html
            |> descendants "table"
            |> Seq.filter (not << (parent >> hasTagName "td"))
            |> Seq.collect (fun table -> table |> elements "tr")
            |> Seq.filter (hasClass "" &&& hasId "")
            |> Seq.map getCallingPoints
            |> Map.ofSeq
        with 
        | :? ParseError -> reraise()
        | exn -> raise <| ParseError(sprintf "Failed to parse departures html from %s:\n%s" htmlUrl html, exn)

    match departuresAndArrivalsTable.CallingAt with
    | None ->

        async {
            let! xml = Http.AsyncRequestString xmlUrl
            return getDeparturesOrArrivals None xml id
        }

    | Some callingAt ->

        async {
            
            let! html = Http.AsyncRequestString htmlUrl
            let html = cleanHtml html

            let callingPointsByTrainId = getCallingPointsByTrain html
            
            let getCallingPoints trainId =
                match Map.tryFind trainId callingPointsByTrainId with
                | Some value -> value
                | _ -> Set.empty

            let! xml = Http.AsyncRequestString xmlUrl

            let extraFilter =
                Seq.filter (fst >> getCallingPoints >> Set.contains callingAt.Name)
         
            return getDeparturesOrArrivals (Some callingAt.Name) xml extraFilter

        }

let getDepartures departuresAndArrivalsTable = 
    
    let synchronizationContext = SynchronizationContext.Current

    async {

        let! token = Async.CancellationToken

        let getDeparture (trainId, (departure:Departure, callingAtFilter, propertyChangedEvent)) = 
            if (!departure.Arrival).IsNone then
                departure.SubscribeToDepartureInformation callingAtFilter propertyChangedEvent synchronizationContext token
            departure

        return! getDeparturesOrArrivals true xmlToDeparture getDeparture departuresAndArrivalsTable
    } |> LazyAsync.fromAsync

let getArrivals departuresAndArrivalsTable = 

    getDeparturesOrArrivals false xmlToArrival snd departuresAndArrivalsTable
    |> LazyAsync.fromAsync
