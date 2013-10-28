module Trains.LiveDepartures.UK

open System
open System.ComponentModel
open System.Globalization
open System.Text.RegularExpressions
open System.Threading
open HtmlAgilityPack
open HtmlAgilityPack.FSharp
open FSharp.Control
open FSharp.Net
open Trains

let private parseInt str = 
    match Int32.TryParse(str, NumberStyles.Integer, CultureInfo.InvariantCulture) with
    | true, i -> Some i
    | false, _ -> None

let private getStatus due (statusCell:HtmlNode) = 
    if statusCell.InnerText.Trim() = "Cancelled" then
        Status.Cancelled
    elif statusCell.InnerText.Trim() = "Delayed" then
        Status.DelayedIndefinitely
    elif statusCell.InnerText.Trim() = "No report" then
        Status.NoReport
    else
        let statusSpan = statusCell.Element("span")
        if statusSpan <> null && statusSpan.InnerText.Contains(" mins late") then
            match statusSpan.InnerText.Replace(" mins late", "") |> parseInt with
            | Some delayMins -> Status.Delayed delayMins
            | _ -> raise <| ParseError(sprintf "Invalid status:\n%s" statusCell.OuterHtml, null)
        else
            Status.OnTime

let private getJourneyElementStatus due (statusCell:HtmlNode) = 
    if statusCell.InnerText.Trim() = "Cancelled" then
        Cancelled
    elif statusCell.InnerText.Trim() = "No report" then
        NoReport
    else
        let statusSpan = statusCell.Element("span")
        let hasDeparted = statusSpan <> null && (statusSpan.InnerText = "Departed" || statusSpan.InnerText = "Arrived")
        let statusSpan = statusCell.Elements("span") |> Seq.last
        if statusSpan <> null && statusSpan.InnerText.Contains(" mins late") then
            match statusSpan.InnerText.Replace(" mins late", "") |> parseInt with
            | Some delayMins -> Delayed (hasDeparted, delayMins)
            | _ -> raise <| ParseError(sprintf "Invalid status:\n%s" statusCell.OuterHtml, null)
        else
            OnTime hasDeparted

let private parsePlatform (cell:HtmlNode) = 
    match cell.InnerText.Trim() with
    | "" -> None
    | platform -> platform |> Some

let private rowToJourneyElement (tr:HtmlNode) = 
    let cells = tr |> elements "td" |> Seq.toArray
    let arrives = cells.[0] |> Time.Parse
    let status = cells.[2] |> getJourneyElementStatus arrives
    let station = cells.[1].InnerText
    let pos = station.IndexOf "Train divides here"
    let station = if pos >= 0 then station.Substring(0, pos) else station
    let isAlternateRoute = tr.Ancestors("tbody") |> Seq.length > 1
    { Arrives = arrives
      Station = (if isAlternateRoute then "* " else "") + station.Trim()
      Status = status
      Platform = cells.[3] |> parsePlatform 
      IsAlternateRoute = isAlternateRoute }

let private getJourneyDetails url = async {

    let! html = Http.AsyncRequestString url
    let html = cleanHtml html

    let getJourneyDetails() = 
        try 
            createDoc html
            |> descendants "tbody"
            |> Seq.collect (fun body -> body |> elements "tr")
            |> Seq.filter (not << (hasClass "callingpoints"))
            |> Seq.map rowToJourneyElement
            |> Seq.toArray
        with 
        | :? ParseError -> reraise()
        | exn -> raise <| ParseError(sprintf "Failed to parse journey details html from %s:\n%s" url html, exn)

    return getJourneyDetails()
}

let rowToDeparture callingAtFilter synchronizationContext token (tr:HtmlNode) =
    
    let cells = tr |> elements "td" |> Seq.toArray
    let destination, destinationDetail = 
        let dest = cells.[1].InnerText.Trim()
        let dest = Regex.Replace(dest, "\s+", " ")
        let pos = dest.IndexOf " via"
        if pos >= 0
        then dest.Substring(0, pos), dest.Substring(pos + 1)
        else
            let pos = dest.IndexOf " (circular route)"
            if pos >= 0
            then dest.Substring(0, pos), dest.Substring(pos + 1)
            else dest, ""
    let due = cells.[0] |> Time.Parse
    let status = cells.[2] |> getStatus due
    let details = 
        let detailsUrl = cells.[4] |> element "a" |> attr "href"
        LazyAsync.fromAsync (getJourneyDetails ("http://ojp.nationalrail.co.uk" + detailsUrl))

    let propertyChangedEvent = Event<_,_>()

    let departure = 
        { Due = due
          Destination = destination
          DestinationDetail = destinationDetail
          Status = status
          Platform = cells.[3] |> parsePlatform
          Details = details
          Arrival = ref None
          PropertyChangedEvent = propertyChangedEvent.Publish }
    
    departure.SubscribeToDepartureInformation callingAtFilter propertyChangedEvent synchronizationContext token
    departure

let private rowToArrival (tr:HtmlNode) =
    let cells = tr.Elements "td" |> Seq.toArray        
    let origin = cells.[1].InnerText.Trim()
    let due = cells.[0] |> Time.Parse
    let status = cells.[2] |> getStatus due
    let details = 
        let detailsUrl = cells.[4] |> element "a" |> attr "href"
        LazyAsync.fromAsync (getJourneyDetails ("http://ojp.nationalrail.co.uk" + detailsUrl))
    { Due = due
      Origin = origin
      Status = status
      Platform = cells.[3] |> parsePlatform
      Details = details }

let getDeparturesFromHtml html callingAtFilter synchronizationContext token = 
    createDoc html
    |> descendants "tbody"
    |> Seq.collect (fun body -> body |> elements "tr")
    |> Seq.map (rowToDeparture callingAtFilter synchronizationContext token)
    |> Seq.toArray

let getDepartures departuresAndArrivalsTable = 

    let url, callingAtFilter = 
        match departuresAndArrivalsTable.CallingAt with
        | None -> sprintf "http://ojp.nationalrail.co.uk/service/ldbboard/dep/%s" departuresAndArrivalsTable.Station.Code, None
        | Some callingAt -> sprintf "http://ojp.nationalrail.co.uk/service/ldbboard/dep/%s/%s/To" departuresAndArrivalsTable.Station.Code callingAt.Code, Some callingAt.Name

    let synchronizationContext = SynchronizationContext.Current

    async {
        let! html = Http.AsyncRequestString url
        let html = cleanHtml html

        let! token = Async.CancellationToken

        let getDepartures() = 
            try 
                getDeparturesFromHtml html callingAtFilter synchronizationContext token
            with 
            | :? ParseError -> reraise()
            | exn -> raise <| ParseError(sprintf "Failed to parse departures html from %s:\n%s" url html, exn)

        return getDepartures()

    } |> LazyAsync.fromAsync

let getArrivals departuresAndArrivalsTable = 

    let url = 
        match departuresAndArrivalsTable.CallingAt with
        | None -> sprintf "http://ojp.nationalrail.co.uk/service/ldbboard/arr/%s" departuresAndArrivalsTable.Station.Code
        | Some callingAt -> sprintf "http://ojp.nationalrail.co.uk/service/ldbboard/arr/%s/%s/From" departuresAndArrivalsTable.Station.Code callingAt.Code 

    async {
        let! html = Http.AsyncRequestString url
        let html = cleanHtml html

        let getArrivals() = 
            try 
                createDoc html
                |> descendants "tbody"
                |> Seq.collect (fun body -> body |> elements "tr")
                |> Seq.map rowToArrival
                |> Seq.toArray
            with 
            | :? ParseError -> reraise()
            | exn -> raise <| ParseError(sprintf "Failed to parse arrivals html from %s:\n%s" url html, exn)

        return getArrivals()

    } |> LazyAsync.fromAsync
