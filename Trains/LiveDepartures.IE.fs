module Trains.LiveDepartures.IE

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
            
let private xmlToDeparture (xml:StationDataXmlT.DomainTypes.ObjStationData) =
    let arrivalInformation = 
        { ArrivalInformation.Due = Time.Create xml.Destinationtime
          Destination = "" // don't display it as it would be repeated
          Status = Status.OnTime } 
    xml.Traincode,
    { Due = Time.Create xml.Schdepart
      Destination = xml.Destination
      DestinationDetail = ""
      Status = if xml.Late > 0 then Status.Delayed xml.Late else Status.OnTime
      Platform = None
      Details = LazyAsync.fromAsync (getJourneyDetails xml.Traincode <| xml.Traindate.ToString("dd MMM yyyy"))
      Arrival = ref <| Some arrivalInformation
      PropertyChangedEvent = Event<_,_>().Publish }

let private xmlToArrival (xml:StationDataXmlT.DomainTypes.ObjStationData) =
    xml.Traincode,
    { Due = Time.Create xml.Scharrival
      Origin = xml.Origin
      Status = if xml.Late > 0 then Status.Delayed xml.Late else Status.OnTime
      Platform = None
      Details = LazyAsync.fromAsync (getJourneyDetails xml.Traincode <| xml.Traindate.ToString("dd MMM yyyy")) }

let private getCallingPoints (tr:HtmlNode) =
    let trim (s:string) = s.Trim()
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
        |> Seq.map (fun tr -> tr 
                              |> elements "td" 
                              |> Seq.skip 1 
                              |> Seq.head 
                              |> innerText
                              |> trim)
        |> Set.ofSeq
    trainId, callingPoints

let private getDeparturesOrArrivals forDepartures mapper (departuresAndArrivalsTable:DeparturesAndArrivalsTable) = 

    let xmlUrl = "http://api.irishrail.ie/realtime/realtime.asmx/getStationDataByCodeXML?StationCode=" + departuresAndArrivalsTable.Station.Code

    let getDeparturesOrArrivals xml extraFilter =
        try 
            let xmlT = StationDataXmlT.Parse xml
            xmlT.GetObjStationDatas()
            |> Seq.filter (fun xml -> xml.Locationtype <> (if forDepartures then LocationType.Destination else LocationType.Origin).ToString())
            |> Seq.map mapper
            |> Seq.toArray
            |> extraFilter
            |> Seq.map snd
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
            return getDeparturesOrArrivals xml id

        } |> LazyAsync.fromAsync

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
            let extraFilter = Seq.filter (fst >> getCallingPoints >> Set.contains callingAt.Name)

            return getDeparturesOrArrivals xml extraFilter

        } |> LazyAsync.fromAsync

let getDepartures = 
    getDeparturesOrArrivals true xmlToDeparture

let getArrivals =
    getDeparturesOrArrivals false xmlToArrival
