module FSharp.GeoUtils

open System

//osgb36 to latitude/longitude converted with http://gridreferencefinder.com/batchConvert/batchConvert.htm

// from https://github.com/synhershko/spatial4n

let private toRadians x = x * (Math.PI / 180.0)
let private toDegrees x = x * (180.0 / Math.PI)    

let private degToKm = 
    let radians2Dist radians radius = 
        radians * radius
    let degrees2Dist degrees radius = 
        radians2Dist (toRadians degrees) radius
    let earthMeanRadiusKm = 6371.0087714
    degrees2Dist 1.0 earthMeanRadiusKm


let private dist (lat1, long1) (lat2, long2) = 
    
    let lat1, long1 = toRadians lat1, toRadians long1
    let lat2, long2 = toRadians lat2, toRadians long2

    let hsinX = sin((long1 - long2) * 0.5)
    let hsinY = sin((lat1 - lat2) * 0.5)
    let h = hsinY * hsinY + (cos(lat1) * cos(lat2) * hsinX * hsinX)
    let dist = 2.0 * atan2 (sqrt h) (sqrt (1.0 - h))

    toDegrees dist * degToKm

type LatLong = 
    { Lat : float
      Long : float }
    static member Create lat long =
        { Lat = lat
          Long = long }
    static member (-) (l1, l2) = dist (l1.Lat, l1.Long) (l2.Lat, l2.Long)
