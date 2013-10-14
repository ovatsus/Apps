#r "System.Net"
#r "../lib/portable/FSharp.Data.dll"
#r "../packages/HtmlAgilityPack-PCL.1.4.6/lib/HtmlAgilityPack-PCL.dll"

#load "HtmlAgilityPack.FSharp.fs"

open System
open FSharp.Net
open HtmlAgilityPack
open HtmlAgilityPack.FSharp

// get a page that lists the stations that start with firstLetter
let getStationListPage firstLetter = 
    "http://www.irishrail.ie/cat_stations_list.jsp?letter=" + (string firstLetter)
    |> Http.AsyncRequestString

// get all the links to stations inside the <ul class="results">
let getStations stationListPage =
    stationListPage
    |> createDoc
    |> descendants "ul"
    |> Seq.filter (hasClass "results")
    |> Seq.head
    |> descendants "a"
    |> Seq.map (attr "href")
    |> Seq.toArray

// get the page for a station
let getStationPage station =
    "http://www.irishrail.ie/" + station
    |> Http.AsyncRequestString

// get the latitude and longitude of a station from the google maps link in the station page
let getCoordinates stationPage = 
    let googleMapsLink = 
        stationPage
        |> createDoc
        |> descendants "div"
        |> Seq.filter (hasId "map-ordnance")
        |> Seq.head
        |> followingSibling "ul"
        |> descendants "a"
        |> Seq.head
        |> attr "href"
    let split (c:char) (s:string) = s.Split c
    let [| "ll" ; coords |] =        
        Uri(googleMapsLink).Query
        |> split '&'
        |> Seq.map (split '=')
        |> Seq.filter (Seq.head >> (=) "ll")
        |> Seq.head
    let [| lat; long |] = coords |> split ',' |> Array.map float
    lat, long

let stationsAndCoords =
    let stations = 
        ['A'..'Z'] 
        |> Seq.map getStationListPage
        |> Async.Parallel
        |> Async.RunSynchronously
        |> Array.collect getStations
    let lat, long = 
        stations
        |> Seq.map getStationPage
        |> Async.Parallel
        |> Async.RunSynchronously
        |> Array.map getCoordinates
        |> Array.unzip
    let stations = 
        stations
        |> Array.map Uri.UnescapeDataString
    Array.zip3 stations lat long

open System.IO

File.WriteAllLines(
    __SOURCE_DIRECTORY__ + "/IrelandStations.csv", 
    stationsAndCoords 
    |> Seq.map (fun (station, lat, long) -> station + "," + (string lat) + "," + (string long))
    |> Seq.append ["Station,Latitude,Longitude"])
