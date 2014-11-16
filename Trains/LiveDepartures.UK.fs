module Trains.LiveDepartures.UK

open System
open System.Net
open System.Threading
open FSharp.Control
open FSharp.Data
open FSharp.Data.HtmlExtensions
open FSharp.Data.HttpRequestHeaders
open Trains

let private wp8UserAgent = "Mozilla/5.0 (compatible; MSIE 10.0; Windows Phone 8.0; Trident/6.0; IEMobile/10.0; ARM; Touch; NOKIA; Lumia 920)"

let private asyncRequestString (url:string) =
    Http.AsyncRequestString(url, headers = [ UserAgent wp8UserAgent ])

let private getStatus due (str:string) = 
    match String.trimEnd "*" str with
    | "On time" | "Starts here" -> Status.OnTime
    | "Cancelled" -> Status.Cancelled
    | "Delayed" -> Status.DelayedIndefinitely
    | "" | "No report" -> Status.NoReport
    | str -> let expected = Time.Parse str
             let delay = expected - due
             if delay.TotalMinutes > 0
             then Status.Delayed delay.TotalMinutes
             else Status.OnTime

let private getJourneyElementStatus getDue (str:string) = 
    match String.trimEnd "*" str with
    | "On time" | "Starts here" -> 
        let departed = Time.Create(DateTime.Now).IsAfter(getDue())
        OnTime departed
    | "Cancelled" -> Cancelled
    | "Delayed" -> DelayedIndefinitely
    | "" | "No report" -> NoReport
    | str -> let expected = Time.Parse str
             let delay = expected - getDue()
             let departed = Time.Create(DateTime.Now).IsAfter(expected)
             if delay.TotalMinutes > 0
             then Delayed (departed, delay.TotalMinutes)
             else OnTime departed

let private parsePlatform (cell:HtmlNode) = 
    match Html.innerText cell |> String.remove "Platform" |> String.trim with
    | "" -> None
    | platform -> platform |> Some

let private parseDueAndStatus parseStatus dueCell =
    let statusStr = dueCell |> HtmlNode.elementsNamed ["small"] |> Seq.head |> Html.innerText
    let dueStr = dueCell |> Html.innerText |> String.trimEnd statusStr |> String.trim
    let due = Time.TryParse dueStr
    let getDue() =
        match due with
        | Some due -> due
        | None -> raise <| ParseError(sprintf "Invalid time:\n%s" dueStr, null)
    due, parseStatus getDue statusStr

let private parseDueAndStatusForceDue parseStatus dueCell =
    let statusStr = dueCell |> HtmlNode.elementsNamed ["small"] |> Seq.head |> Html.innerText
    let dueStr = dueCell |> Html.innerText |> String.trimEnd statusStr |> String.trim
    let due = Time.Parse dueStr
    due, parseStatus due statusStr

let private isDisruption (li:HtmlNode) = 
    let link = li |> HtmlNode.elementsNamed ["a"] |> Seq.tryHead
    link.IsSome && String.contains "/disruption" link.Value?href

let private rowToJourneyElement platform due ((li:HtmlNode), isAlternateRoute) = 

    let cells = li |> HtmlNode.elementsNamed ["span"]
    if cells.Length < 2 then None else

    let station = 
        let station = cells.[1] |> Html.innerText
        let pos = station.IndexOf "Train divides here"
        if pos >= 0 then station.Substring(0, pos) |> String.trim else station
    
    let arrives, status = 
        cells.[0] |> parseDueAndStatus getJourneyElementStatus

    let platform = if arrives = Some due then platform else None

    let journeyElement = { Arrives = arrives
                           Station = (if isAlternateRoute then "* " else "") + (String.trim station)
                           Status = status
                           Platform = platform
                           IsAlternateRoute = isAlternateRoute }
    
    Some journeyElement

let internal getJourneyDetailsFromHtml platform due html = 
    let foundHr = ref false
    html
    |> HtmlDocument.Parse
    |> HtmlDocument.descendantsNamed false ["li"; "hr"]
    |> Seq.map (fun elem -> if elem.Name() = "hr" then foundHr := true
                            elem, !foundHr)
    |> Seq.filter (fst >> isDisruption >> not)
    |> Seq.choose (rowToJourneyElement platform due)
    |> Seq.toArray

let mutable LastHtml = ""

let private getJourneyDetails platform due url = async {

    let! html = asyncRequestString url
    let html = Html.clean html
    LastHtml <- html

    let getJourneyDetails() = 
        try 
            getJourneyDetailsFromHtml platform due html
        with 
        | _ when html.IndexOf("Wi-Fi", StringComparison.OrdinalIgnoreCase) > 0 ||
                 html.IndexOf("WiFi", StringComparison.OrdinalIgnoreCase) > 0 ->
            raise <| new WebException()
        | exn -> raise <| ParseError(sprintf "Failed to parse journey details html from %s:\n%s\n" url html, exn)

    return getJourneyDetails()
}

let private rowToDeparture callingAtFilter synchronizationContext token i (li:HtmlNode) =
    
    let link = li |> HtmlNode.elementsNamed ["a" ] |> Seq.tryHead

    match link with
    | None -> None
    | Some link ->

        let cells = link |> HtmlNode.elementsNamed ["span"]

        let platform = cells |> List.tryFind (HtmlNode.hasClass "platform") |> Option.bind parsePlatform
    
        let length = cells.Length - match platform with Some _ -> 1 | None -> 0
   
        if length < 2 then None else

        let due, status = 
            cells.[0] |> parseDueAndStatusForceDue getStatus

        let destination, destinationDetail = 
            let dest = (cells.[1] |> Html.innerText).Split([| Environment.NewLine |], StringSplitOptions.RemoveEmptyEntries)
            dest.[0], dest |> Seq.skip 1 |> String.concat "\n" |> String.replace " via" "via"  |> String.replace "\nvia" " via"

        let details = 
            let detailsUrl = link?href
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
    
        // only fetch the arrival time for the first 10 departures
        if i < 10 then
            departure.SubscribeToDepartureInformation callingAtFilter propertyChangedEvent synchronizationContext token

        Some departure

let private rowToArrival (li:HtmlNode) =

    let link = li |> HtmlNode.elementsNamed ["a"] |> Seq.tryHead

    match link with
    | None -> None
    | Some link ->

        let cells = link |> HtmlNode.elementsNamed ["span"]

        let platform = cells |> List.tryFind (HtmlNode.hasClass "platform") |> Option.bind parsePlatform
    
        let length = cells.Length - match platform with Some _ -> 1 | None -> 0
   
        if length < 2 then None else

        let due, status = 
            cells.[0] |> parseDueAndStatusForceDue getStatus

        let origin = cells.[1] |> Html.innerText

        let details = 
            let detailsUrl = link?href
            LazyAsync.fromAsync (getJourneyDetails platform due ("http://m.nationalrail.co.uk" + detailsUrl))

        let arrival = { Due = due
                        Origin = origin
                        Status = status
                        Platform = platform
                        Details = details }

        Some arrival

let internal getDeparturesFromHtml html callingAtFilter synchronizationContext token = 
    html
    |> HtmlDocument.Parse
    |> HtmlDocument.descendantsNamed false ["li"]
    |> Seq.filter (not << isDisruption)
    |> Seq.mapi (rowToDeparture callingAtFilter synchronizationContext token)
    |> Seq.choose id
    |> Seq.toArray

let getDepartures departuresAndArrivalsTable = 

    let url, callingAtFilter = 
        match departuresAndArrivalsTable.CallingAt with
        | None -> sprintf "http://m.nationalrail.co.uk/pj/ldbboard/dep/%s" departuresAndArrivalsTable.Station.Code, None
        | Some callingAt -> sprintf "http://m.nationalrail.co.uk/pj/ldbboard/dep/%s/%s/To" departuresAndArrivalsTable.Station.Code callingAt.Code, Some callingAt.Name

    let synchronizationContext = SynchronizationContext.Current

    async {
        let! html = asyncRequestString url
        let html = Html.clean html
        LastHtml <- html

        let! token = Async.CancellationToken

        let getDepartures() = 
            try 
                getDeparturesFromHtml html callingAtFilter synchronizationContext token
            with 
            | _ when html.IndexOf("Wi-Fi", StringComparison.OrdinalIgnoreCase) > 0 ||
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
        let html = Html.clean html
        LastHtml <- html

        let getArrivals() = 
            try 
                html
                |> HtmlDocument.Parse
                |> HtmlDocument.descendantsNamed false ["li"]
                |> Seq.filter (not << isDisruption)
                |> Seq.choose rowToArrival
                |> Seq.toArray
            with 
            | _ when html.IndexOf("Wi-Fi", StringComparison.OrdinalIgnoreCase) > 0 ||
                     html.IndexOf("WiFi", StringComparison.OrdinalIgnoreCase) > 0 ->
                raise <| new WebException()
            | exn -> raise <| ParseError(sprintf "Failed to parse arrivals html from %s:\n%s\n" url html, exn)

        return getArrivals()

    } |> LazyAsync.fromAsync
