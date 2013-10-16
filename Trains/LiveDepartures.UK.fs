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

let private parseTime (cell:HtmlNode) = 
    let time = cell.InnerText
    let pos = time.IndexOf ':'
    if pos >= 0 then
        let hours = time.Substring(0, pos) |> parseInt
        let minutes = time.Substring(pos+1) |> parseInt
        match hours, minutes with
        | Some hours, Some minutes -> Time.Create(hours, minutes)
        | _ -> raise <| ParseError(sprintf "Invalid time:\n%s" cell.OuterHtml, null)
    else raise <| ParseError(sprintf "Invalid time:\n%s" cell.OuterHtml, null)

let private parsePlatform (cell:HtmlNode) = 
    match cell.InnerText.Trim() with
    | "" -> None
    | platform -> platform |> Some

let private rowToJourneyElement (tr:HtmlNode) = 
    let cells = tr.Elements "td" |> Seq.toArray
    let departs = cells.[0] |> parseTime
    let status = cells.[2] |> getJourneyElementStatus departs
    let station = cells.[1].InnerText
    let pos = station.IndexOf "Train divides here"
    let station = if pos >= 0 then station.Substring(0, pos) else station
    let isAlternateRoute = tr.Ancestors("tbody") |> Seq.length > 1
    { Departs = departs
      Station = (if isAlternateRoute then "* " else "") + station.Trim()
      Status = status
      Platform = cells.[3] |> parsePlatform 
      IsAlternateRoute = isAlternateRoute }

// reduce size of bug reports
let private cleanHtml (str:string) = 
    let replace pattern (replacement:string) str = Regex.Replace(str, pattern, replacement)
    str.Replace("\r", null).Replace("\n", null)
    |> replace ">\s*<" "><"
    |> replace "<head>.+?</head>" ""
    |> replace "<script[^>]*>.+?</script>" ""
    |> replace "<noscript>.+?</noscript>" ""

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
        | exn -> raise <| ParseError(sprintf "Failed to parse journey details html from %s:\n%s" url (html.Trim()), exn)

    return getJourneyDetails()
}

let private rowToDeparture arrivalInformation propertyChangedEvent (tr:HtmlNode) =
    let cells = tr.Elements "td" |> Seq.toArray        
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
    let due = cells.[0] |> parseTime
    let status = cells.[2] |> getStatus due
    let details = 
        let detailsUrl = cells.[4] |> element "a" |> attr "href"
        LazyAsync.fromAsync (getJourneyDetails ("http://ojp.nationalrail.co.uk" + detailsUrl))
    { Due = due
      Destination = destination
      DestinationDetail = destinationDetail
      Status = status
      Platform = cells.[3] |> parsePlatform
      Details = details
      Arrival = arrivalInformation
      PropertyChangedEvent = propertyChangedEvent }

let getDepartures (departureType:DepartureType) departuresTable = 

    let synchronizationContext = SynchronizationContext.Current

    let rowToDeparture token = 
        match departuresTable.CallingAt, departureType with
        | _, Arrival
        | None, _ -> rowToDeparture (ref None) (Event<_,_>().Publish)
        | Some callingAt, _ -> fun row ->
            
            let arrivalInformation = ref None
            let propertyChangedEvent = Event<_,_>()
            
            let departure = row |> rowToDeparture arrivalInformation propertyChangedEvent.Publish

            let postArrivalInformation (journeyElements:JourneyElement[]) index = 
                
                let journeyElement = journeyElements.[index]

                let destination = 
                    if index = journeyElements.Length - 1 then "" // don't display it as it would be repeated
                    else journeyElement.Station // display the smaller (journeyElement.Station.Length <= calligAt.Name.Length)                            

                let status =
                    match journeyElement.Status with
                    | Delayed (_, mins) -> Status.Delayed mins
                    | Cancelled -> failwith "Not possible"
                    | _ -> Status.OnTime

                arrivalInformation := 
                    Some { Due = journeyElement.Departs
                           Destination = destination
                           Status = status }
                
                let triggerProperyChanged _ = 
                    propertyChangedEvent.Trigger(departure, PropertyChangedEventArgs "Arrival")
                    propertyChangedEvent.Trigger(departure, PropertyChangedEventArgs "ArrivalIsKnown")
                
                synchronizationContext.Post(SendOrPostCallback(triggerProperyChanged), null)
            
            let onJourneyElementsObtained (journeyElements:JourneyElement[]) =
                let index = journeyElements 
                            |> Array.tryFindIndex (fun journeyElement -> journeyElement.Station = callingAt.Name)
                let index = 
                    match index with
                    | Some _ -> index
                    | None -> journeyElements 
                              |> Array.tryFindIndex (fun journeyElement -> // Sometimes there's no 100% match, eg: Farringdon vs Farringdon (London)
                                                                           callingAt.Name.StartsWith journeyElement.Station)
                index |> Option.iter (postArrivalInformation journeyElements)

            if departure.Status <> Status.Cancelled then
                departure.Details.GetValueAsync (Some token) onJourneyElementsObtained ignore ignore |> ignore

            departure

    let url = 
        match departuresTable.CallingAt with
        | None -> sprintf "http://ojp.nationalrail.co.uk/service/ldbboard/%s/%s" (departureType.ToString()) departuresTable.Station.Code
        | Some callingAt -> sprintf "http://ojp.nationalrail.co.uk/service/ldbboard/%s/%s/%s/To" (departureType.ToString()) departuresTable.Station.Code callingAt.Code 

    async {
        let! html = Http.AsyncRequestString url
        let html = cleanHtml html

        let! token = Async.CancellationToken

        let getDepartures() = 
            try 
                createDoc html
                |> descendants "tbody"
                |> Seq.collect (fun body -> body |> elements "tr")
                |> Seq.map (rowToDeparture token)
                |> Seq.toArray
            with 
            | :? ParseError -> reraise()
            | exn -> raise <| ParseError(sprintf "Failed to parse departures html from %s:\n%s" url (html.Trim()), exn)

        return getDepartures()

    } |> LazyAsync.fromAsync
