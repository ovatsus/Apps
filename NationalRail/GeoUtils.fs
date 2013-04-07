module FSharp.GeoUtils

open System

let private toRadians x = x * (Math.PI / 180.0)
let private toDegrees x = x * (180.0 / Math.PI)    

let private airy1830Ellipsoid_semiMajorAxis = 6377563.396
let private airy1830Ellipsoid_semiMinorAxis = 6356256.909

let private airy1830Ellipsoid_EccentricitySquared = 
    let semiMajorAxisSquared = airy1830Ellipsoid_semiMajorAxis * airy1830Ellipsoid_semiMajorAxis
    let semiMinorAxisSquared = airy1830Ellipsoid_semiMinorAxis * airy1830Ellipsoid_semiMinorAxis
    (semiMajorAxisSquared - semiMinorAxisSquared) / semiMajorAxisSquared

// based on http://www.doogal.co.uk/dotnetcoords.php
let private toLatLong easting northing =

    let sinSquared x = let sinX = sin x in sinX * sinX
    let tanSquared x = let tanX = tan x in tanX * tanX
    let sec x = 1.0 / cos x
    let pow = Math.Pow

    let OSGB_F0 = 0.9996012717
    let N0 = -100000.0
    let E0 = 400000.0
    let phi0 = toRadians 49.0
    let lambda0 = toRadians -2.0
    let a = airy1830Ellipsoid_semiMajorAxis
    let b = airy1830Ellipsoid_semiMinorAxis
    let eSquared = airy1830Ellipsoid_EccentricitySquared
    let E = easting
    let N = northing
    let n = (a - b) / (a + b)
    let mutable M = 0.0
    let mutable phiPrime = ((N - N0) / (a * OSGB_F0)) + phi0
    let mutable continueLoop = true
    while continueLoop do
        M <- (b * OSGB_F0)
            * (((1.0 + n + ((5.0 / 4.0) * n * n) + ((5.0 / 4.0) * n * n * n)) * (phiPrime - phi0))
                - (((3.0 * n) + (3.0 * n * n) + ((21.0 / 8.0) * n * n * n))
                    * sin(phiPrime - phi0) * cos(phiPrime + phi0))
                + ((((15.0 / 8.0) * n * n) + ((15.0 / 8.0) * n * n * n))
                    * sin(2.0 * (phiPrime - phi0)) * cos(2.0 * (phiPrime + phi0))) - (((35.0 / 24.0) * n * n * n)
                * sin(3.0 * (phiPrime - phi0)) * cos(3.0 * (phiPrime + phi0))))
        phiPrime <- phiPrime + (N - N0 - M) / (a * OSGB_F0)    
        continueLoop <- N - N0 - M >= 0.001
    let v = a * OSGB_F0 * pow(1.0 - eSquared * sinSquared phiPrime, -0.5)
    let rho = a * OSGB_F0 * (1.0 - eSquared) * pow(1.0 - eSquared * sinSquared phiPrime, -1.5)
    let etaSquared = (v / rho) - 1.0
    let VII = Math.Tan(phiPrime) / (2.0 * rho * v)
    let VIII = (Math.Tan(phiPrime) / (24.0 * rho * pow(v, 3.0))) * (5.0 + (3.0 * tanSquared phiPrime) + etaSquared - (9.0 * tanSquared phiPrime * etaSquared))
    let IX = (tan(phiPrime) / (720.0 * rho * pow(v, 5.0))) * (61.0 + (90.0 * tanSquared phiPrime) + (45.0 * tanSquared phiPrime * tanSquared phiPrime))
    let X = sec(phiPrime) / v
    let XI = (sec(phiPrime) / (6.0 * v * v * v)) * ((v / rho) + (2.0 * tanSquared phiPrime))
    let XII = (sec(phiPrime) / (120.0 * pow(v, 5.0))) * (5.0 + (28.0 * tanSquared phiPrime) + (24.0 * tanSquared phiPrime * tanSquared phiPrime))
    let XIIA = (sec(phiPrime) / (5040.0 * pow(v, 7.0))) * (61.0 + (662.0 * tanSquared phiPrime) + (1320.0 * tanSquared phiPrime * tanSquared phiPrime) + (720.0 * tanSquared phiPrime * tanSquared phiPrime * tanSquared phiPrime))
    let phi = phiPrime - (VII * pow(E - E0, 2.0)) + (VIII * pow(E - E0, 4.0)) - (IX * pow(E - E0, 6.0))
    let lambda = lambda0 + (X * (E - E0)) - (XI * pow(E - E0, 3.0)) + (XII * pow(E - E0, 5.0)) - (XIIA * pow(E - E0, 7.0))

    toDegrees phi, toDegrees lambda

let private degToKm = 
    let radians2Dist radians radius = 
        radians * radius
    let degrees2Dist degrees radius = 
        radians2Dist (toRadians degrees) radius
    let earthMeanRadiusKm = 6371.0087714
    degrees2Dist 1.0 earthMeanRadiusKm

// from https://github.com/synhershko/spatial4n
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
    static member FromUTM easting northing =
        let lat, long = toLatLong (float easting) (float northing)
        { Lat = lat
          Long = long }
    static member Create lat long =
        { Lat = lat
          Long = long }
    static member (-) (l1, l2) = dist (l1.Lat, l1.Long) (l2.Lat, l2.Long)
