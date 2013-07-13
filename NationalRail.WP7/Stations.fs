namespace NationalRail

open System.Reflection
open FSharp.Control
#if SILVERLIGHT
open FSharp.Data.Csv
open FSharp.Data.Csv.Extensions
#else
open FSharp.Data
#endif
open FSharp.GeoUtils

type Station =
    { Code : string
      Name : string
      Location : LatLong }

module Stations = 

    open FSharp.Data.Csv
    open FSharp.Data.Csv.Extensions

    let private stationInfo = ref None

    let private getStationInfo() = 

        match !stationInfo with
        | None ->

            let allStations = 
                //from http://www.data.gov.uk/dataset/naptan
                //osgb36 to latitude/longitude converted with http://gridreferencefinder.com/batchConvert/batchConvert.htm
                use stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("RailReferences.csv")
#if SILVERLIGHT
                let csvFile = CsvFile.Load stream
                csvFile.Data
                |> Seq.groupBy (fun station -> station?CrsCode)
                |> Seq.map (fun (code, stations) -> 
                    let station = 
                        stations 
                        |> Seq.sortBy (fun station -> - station?RevisionNumber.AsInteger()) 
                        |> Seq.head
                    { Code = code
                      Name = station?StationName.Replace(" Rail Station", null)
                      Location = LatLong.Create (station?Latitude.AsFloat()) (station?Longitude.AsFloat()) })
                |> Seq.toList
#else
                let csvFile = CsvProvider<"../NationalRail.WP7/RailReferences.csv">.Load stream
                csvFile.Data
                |> Seq.groupBy (fun station -> station.CrsCode)
                |> Seq.map (fun (code, stations) -> 
                    let station = 
                        stations 
                        |> Seq.sortBy (fun station -> - station.RevisionNumber) 
                        |> Seq.head
                    { Code = code
                      Name = station.StationName.Replace(" Rail Station", null)
                      Location = LatLong.Create (float station.Latitude) (float station.Longitude) })
                |> Seq.toList
#endif

            let stationsByCode =
                allStations 
                |> Seq.map (fun station -> station.Code, station) 
                |> dict 

            stationInfo := Some (allStations, stationsByCode)
            allStations, stationsByCode

        | Some stationInfo -> stationInfo

    [<CompiledName("GetNearest")>]
    let getNearest currentPosition limit useMilesInsteadOfKMs = 
    
        async {

            let allStations, _ = getStationInfo()

            let nearestStations =
                allStations
                |> Seq.map (fun station -> station.Location - currentPosition, station)
                |> Seq.filter (fun (dist, _) -> dist < 1000.0)
                |> Seq.sortBy (fun (dist, station) -> dist, station.Name)
                |> Seq.truncate limit
                |> Seq.map (fun (distance, station) -> 
                    if useMilesInsteadOfKMs then
                        let distance = distance * 0.621371192
                        sprintf "%.1f mi" distance, station
                    else
                        sprintf "%.1f km" distance, station)
                |> Seq.toArray

            return nearestStations

        } |> LazyAsync.fromAsync

    [<CompiledName("GetAll")>]
    let getAll() =     
        let allStations, _ = getStationInfo()
        allStations
        |> Seq.sortBy (fun station -> station.Name)
        |> Seq.toArray

    [<CompiledName("Get")>]
    let get stationCode =
        let _, stationsByCode = getStationInfo()
        stationsByCode.[stationCode]