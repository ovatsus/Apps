namespace global

open System
open System.Globalization
open System.Text.RegularExpressions
open FSharp.Data

module Resources =

    let mutable getResourceStreamFunc = Unchecked.defaultof<Func<string, IO.Stream>>
    let getResourceStream resourceName = getResourceStreamFunc.Invoke resourceName

module Seq =
    
    let inline tryHead (seq:seq<_>) = 
        use it = seq.GetEnumerator()
        if it.MoveNext() then
            Some it.Current
        else
            None

module String =
    
    let inline trim (s:string) = s.Trim()
    let inline trimEnd value (s:string) = if s.EndsWith value then s.Substring(0, s.Length - value.Length) else s
    let inline replace (value:string) replacement (str:string) = str.Replace(value, replacement)
    let inline remove toRemove (s:string) = if s = "" then "" else s.Replace(toRemove, "")
    let inline replaceRegex pattern (replacement:string) s = Regex.Replace(s, pattern, replacement)
    let inline removeRegex pattern (s:string) = Regex.Replace(s, pattern, "")
    let inline endsWith suffix (s:string) = s.EndsWith suffix
    let inline contains prefix (s:string) =  s.IndexOf(prefix, StringComparison.OrdinalIgnoreCase) >= 0

module Html = 

    let innerText (node:HtmlNode) = 
        node |> HtmlNode.innerText |> String.trim 

    let followingSibling name (node : HtmlNode, parents: HtmlNode list) =
        parents 
        |> List.head
        |> HtmlNode.elements
        |> Seq.skipWhile ((<>) node)
        |> Seq.skip 1
        |> Seq.tryFind (HtmlNode.hasName name)

    // reduce size of bug reports
    let clean (str:string) = 
        str
        |> String.remove "\r"
        |> String.remove "\n"
        |> String.trim
        |> String.replaceRegex ">\s*<" "><"
        |> String.removeRegex "<head>.+?</head>"
        |> String.removeRegex "<script[^>]*>.+?</script>"
        |> String.removeRegex "<script[^>]*></script>"
        |> String.removeRegex "<noscript>.+?</noscript>"
