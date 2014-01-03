module Trains.LiveDepartures.UK

open System
open System.ComponentModel
open System.Globalization
open System.Net
open System.Text.RegularExpressions
open System.Threading
open HtmlAgilityPack
open HtmlAgilityPack.FSharp
open FSharp.Control
open FSharp.Net
open Trains

let wp8UserAgent = "Mozilla/5.0 (compatible; MSIE 10.0; Windows Phone 8.0; Trident/6.0; IEMobile/10.0; ARM; Touch; NOKIA; Lumia 920)"

let private asyncRequestString (url:string) =
    Http.AsyncRequestString(url, headers = ["User-Agent", wp8UserAgent])

let private getStatus (due:Time) (str:string) = 
    match remove "*" str with
    | "On time" | "Starts here" -> Status.OnTime
    | "Cancelled" -> Status.Cancelled
    | "Delayed" -> Status.DelayedIndefinitely
    | "" | "No report" -> Status.NoReport
    | str -> let expected = Time.Parse str
             let delay = expected - due 
             if delay.TotalMinutes > 0
             then Status.Delayed delay.TotalMinutes
             else Status.OnTime

let private getJourneyElementStatus (due:Time option) (str:string) = 
    match remove "*" str with
    | "On time" | "Starts here" -> 
        let departed = Time.Create(DateTime.Now) >= due.Value
        OnTime departed
    | "Cancelled" -> Cancelled
    | "Delayed" -> DelayedIndefinitely
    | "" | "No report" -> NoReport
    | str -> let expected = Time.Parse str
             let delay = expected - due.Value
             let departed = Time.Create(DateTime.Now) >= expected
             if delay.TotalMinutes > 0
             then Delayed (departed, delay.TotalMinutes)
             else OnTime departed

let private parsePlatform (cell:HtmlNode) = 
    match innerText cell |> remove "Platform\n" with
    | "" -> None
    | platform -> platform |> Some

let private rowToJourneyElement platform due (li:HtmlNode) = 

    let cells = li |> elements "span" |> Seq.toArray

    let station, isAlternateRoute = 
        let station = cells.[1] |> innerText
        let pos = station.IndexOf "Train divides here"
        let station = if pos >= 0 then station.Substring(0, pos) |> trim else station
        let isAlternateRoute = li |> parent |> precedingSibling "hr" <> null
        station, isAlternateRoute
    
    let arrives, status =
        let dueCell = cells.[0]
        let statusStr = dueCell |> element "small" |> innerText
        let dueStr = dueCell |> innerText |> remove statusStr
        let due = Time.TryParse dueStr
        due, getJourneyElementStatus due statusStr

    let platform = if arrives = Some due then platform else None

    { Arrives = arrives
      Station = (if isAlternateRoute then "* " else "") + (trim station)
      Status = status
      Platform = platform
      IsAlternateRoute = isAlternateRoute }


let internal getJourneyDetailsFromHtml platform due html = 
    createDoc html
    |> descendants "li"
    |> Seq.map (rowToJourneyElement platform due)
    |> Seq.toArray

let private getJourneyDetails platform due url = async {

    let! html = asyncRequestString url
    let html = cleanHtml html

    let getJourneyDetails() = 
        try 
            getJourneyDetailsFromHtml platform due html
        with 
        | exn when html.IndexOf("Wi-Fi", StringComparison.OrdinalIgnoreCase) > 0 ||
                   html.IndexOf("WiFi", StringComparison.OrdinalIgnoreCase) > 0 ->
            raise <| new WebException()
        | exn -> raise <| ParseError(sprintf "Failed to parse journey details html from %s:\n%s\n" url html, exn)

    return getJourneyDetails()
}

let private rowToDeparture callingAtFilter synchronizationContext token i (li:HtmlNode) =
    
    let link = li |> element "a" 
    let cells = link |> elements "span" |> Seq.toArray

    let destination, destinationDetail = 
        let dest = (cells.[1] |> innerText).Split('\n')
        dest.[0], dest |> Seq.skip 1 |> String.concat "\n" |> replace " via" "via"  |> replace "\nvia" " via"

    let due, status =
        let dueCell = cells.[0]
        let statusStr = dueCell |> element "small" |> innerText
        let dueStr = dueCell |> innerText |> remove statusStr
        let due = Time.Parse dueStr
        due, getStatus due statusStr

    let platform = cells |> Array.tryFind (hasClass "platform") |> Option.bind parsePlatform

    let details = 
        let detailsUrl = link |> attr "href"
        LazyAsync.fromAsync (getJourneyDetails platform due ("http://m.nationalrail.co.uk" + detailsUrl))

    let propertyChangedEvent = Event<_,_>()

    let departure = 
        { Due = due
          Destination = destination
          DestinationDetail = destinationDetail
          Status = status
          Platform = platform
          Details = details
          Arrival = ref None
          PropertyChangedEvent = propertyChangedEvent.Publish }
    
    // only fetch the arrival time for the first 4 departures
    if i < 4 then
        departure.SubscribeToDepartureInformation callingAtFilter propertyChangedEvent synchronizationContext token

    departure

let private rowToArrival (li:HtmlNode) =

    let link = li |> element "a" 
    let cells = link |> elements "span" |> Seq.toArray

    let origin = cells.[1] |> innerText
    
    let due, status =
        let dueCell = cells.[0]
        let statusStr = dueCell |> element "small" |> innerText
        let dueStr = dueCell |> innerText |> remove statusStr
        let due = Time.Parse dueStr
        due, getStatus due statusStr

    let platform = cells |> Array.tryFind (hasClass "platform") |> Option.bind parsePlatform

    let details = 
        let detailsUrl = link |> attr "href"
        LazyAsync.fromAsync (getJourneyDetails platform due ("http://m.nationalrail.co.uk" + detailsUrl))

    { Due = due
      Origin = origin
      Status = status
      Platform = platform
      Details = details }

let internal getDeparturesFromHtml html callingAtFilter synchronizationContext token = 
    createDoc html
    |> descendants "li"
    |> Seq.mapi (rowToDeparture callingAtFilter synchronizationContext token)
    |> Seq.toArray

let getDepartures departuresAndArrivalsTable = 

    let url, callingAtFilter = 
        match departuresAndArrivalsTable.CallingAt with
        | None -> sprintf "http://m.nationalrail.co.uk/pj/ldbboard/dep/%s" departuresAndArrivalsTable.Station.Code, None
        | Some callingAt -> sprintf "http://m.nationalrail.co.uk/pj/ldbboard/dep/%s/%s/To" departuresAndArrivalsTable.Station.Code callingAt.Code, Some callingAt.Name

    let synchronizationContext = SynchronizationContext.Current

    async {
        let! html = asyncRequestString url
        let html = cleanHtml html

        let! token = Async.CancellationToken

        let getDepartures() = 
            try 
                getDeparturesFromHtml html callingAtFilter synchronizationContext token
            with 
            | exn when html.IndexOf("Wi-Fi", StringComparison.OrdinalIgnoreCase) > 0 ||
                       html.IndexOf("WiFi", StringComparison.OrdinalIgnoreCase) > 0 ->
                raise <| new WebException()
            | exn -> raise <| ParseError(sprintf "Failed to parse departures html from %s:\n%s\n" url html, exn)

        return getDepartures()

    } |> LazyAsync.fromAsync

let getArrivals departuresAndArrivalsTable = 

    let url = 
        match departuresAndArrivalsTable.CallingAt with
        | None -> sprintf "http://m.nationalrail.co.uk/pj/ldbboard/arr/%s" departuresAndArrivalsTable.Station.Code
        | Some callingAt -> sprintf "http://m.nationalrail.co.uk/pj/ldbboard/arr/%s/%s/From" departuresAndArrivalsTable.Station.Code callingAt.Code 

    async {
        let! html = asyncRequestString url
        let html = cleanHtml html

        let getArrivals() = 
            try 
                createDoc html
                |> descendants "li"
                |> Seq.map rowToArrival
                |> Seq.toArray
            with 
            | exn when html.IndexOf("Wi-Fi", StringComparison.OrdinalIgnoreCase) > 0 ||
                       html.IndexOf("WiFi", StringComparison.OrdinalIgnoreCase) > 0 ->
                raise <| new WebException()
            | exn -> raise <| ParseError(sprintf "Failed to parse arrivals html from %s:\n%s\n" url html, exn)

        return getArrivals()

    } |> LazyAsync.fromAsync
