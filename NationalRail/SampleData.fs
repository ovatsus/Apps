namespace NationalRail

open System
open FSharp.Control
open FSharp.GeoUtils

type SampleData() =

    let stations = Stations.getAll() |> Array.sortBy (fun s -> s.Name.Length)
    
    let s1 = stations.[stations.Length / 2]
    let s2 = stations.[0]
    let s3 = stations.[stations.Length - 1]
    let s4 = stations.[2 * stations.Length / 3]

    let t1 = Time.FromHoursAndMinutes(1,1)
    let t2 = Time.FromHoursAndMinutes(23,59)
    let t3 = Time.FromHoursAndMinutes(16,47)
    let t4 = Time.FromHoursAndMinutes(20,8)

    let details = LazyAsync.fromValue [| |]

    member __.NearestStations = [ "1.2 km", s1 
                                  "3.4 km", s2 
                                  "5.6 km", s3 
                                  "7.8 km", s4
                                  "9.0 km", s1 ]

    member __.RecentStations = [ DeparturesTable.Create(s1)
                                 DeparturesTable.Create(s2, s3)
                                 DeparturesTable.Create(s4)
                                 DeparturesTable.Create(s3, s1) ]
    
    member __.AllStations = [ s1; s2; s3; s4 ]
    
    member __.Departures = [ { Due = t1
                               Destination = s1.Name
                               DestinationDetail = "via " + s2.Name
                               Status = Status.OnTime 
                               Platform = Some "22C"
                               Details = details }
                             { Due = t2
                               Destination = s2.Name
                               DestinationDetail = ""
                               Status = Status.OnTime
                               Platform = None
                               Details = details }
                             { Due = t3
                               Destination = s3.Name
                               DestinationDetail = "via " + s1.Name + " & " + s2.Name + " via " + s4.Name
                               Status = Status.Delayed 1
                               Platform = Some "4"
                               Details = details }
                             { Due = t4
                               Destination = s4.Name
                               DestinationDetail = ""
                               Status = Status.Delayed 130
                               Platform = None
                               Details = details }
                             { Due = t1
                               Destination = s1.Name
                               DestinationDetail = "via " + s3.Name
                               Status = Status.Cancelled
                               Platform = Some "12"
                               Details = details }
                             { Due = t2
                               Destination = s2.Name
                               DestinationDetail = ""
                               Status = Status.Cancelled
                               Platform = None
                               Details = details } ]

    member x.Arrivals = x.Departures
    
    member __.LiveProgress = [ { Departs = t1
                                 Station = s1.Name
                                 Status = OnTime true
                                 Platform = None
                                 IsAlternateRoute = false }
                               { Departs = t2
                                 Station = s2.Name
                                 Status = Delayed (true, 5)
                                 Platform = Some "6"
                                 IsAlternateRoute = false }
                               { Departs = t3
                                 Station = s3.Name
                                 Status = NoReport
                                 Platform = None
                                 IsAlternateRoute = false }
                               { Departs = t4
                                 Station = s4.Name
                                 Status = Cancelled
                                 Platform = Some "20D"
                                 IsAlternateRoute = false }
                               { Departs = t1
                                 Station = "* " + s1.Name
                                 Status = OnTime false
                                 Platform = None
                                 IsAlternateRoute = true }
                               { Departs = t1
                                 Station = "* " + s2.Name
                                 Status = Delayed (false, 5)
                                 Platform = Some "21"
                                 IsAlternateRoute = true } ]

    member __.LastUpdated = "last updated at " + DateTime.Now.ToString("HH:mm:ss")
