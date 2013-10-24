module HtmlAgilityPack.FSharp

open HtmlAgilityPack

type HtmlNode with 
    
    member x.FollowingSibling name = 
        let sibling = x.NextSibling
        if sibling = null then
            null
        elif sibling.Name = name then
            sibling
        else 
            sibling.FollowingSibling name
    
    member x.FollowingSiblings name = seq {
        let sibling = x.NextSibling
        if sibling <> null then
            if sibling.Name = name then
                yield sibling
            yield! sibling.FollowingSiblings name
    }

    member x.PrecedingSibling name = 
        let sibling = x.PreviousSibling
        if sibling = null then
            null
        elif sibling.Name = name then
            sibling
        else 
            sibling.PrecedingSibling name
    
    member x.PrecedingSiblings name = seq {
        let sibling = x.PreviousSibling
        if sibling <> null then
            if sibling.Name = name then
                yield sibling
            yield! sibling.PrecedingSiblings name
    }

let parent (node : HtmlNode) = 
    node.ParentNode

let element name (node : HtmlNode) = 
    node.Element name

let elements name (node : HtmlNode) = 
    node.Elements name

let descendants name (node : HtmlNode) = 
    node.Descendants name

let descendantsAndSelf name (node : HtmlNode) = 
    node.DescendantsAndSelf name

let ancestors name (node : HtmlNode) = 
    node.Ancestors name

let ancestorsAndSelf name (node : HtmlNode) = 
    node.AncestorsAndSelf name

let followingSibling name (node : HtmlNode) = 
    node.FollowingSibling name

let followingSiblings name (node : HtmlNode) = 
    node.FollowingSiblings name

let precedingSibling name (node : HtmlNode) = 
    node.PrecedingSibling name

let precedingSiblings name (node : HtmlNode) = 
    node.PrecedingSiblings name

let inline innerText (node : HtmlNode) = 
    node.InnerText

let inline attr name (node : HtmlNode) = 
    node.GetAttributeValue(name, "")

let inline (?) (node : HtmlNode) name = 
    attr name node

let inline hasAttr name value node = 
    attr name node = value

let inline hasId value node = 
    hasAttr "id" value node

let inline hasClass value node = 
    hasAttr "class" value node

let inline hasTagName tagName (node : HtmlNode) = 
    node.Name.ToLowerInvariant() = tagName

let inline hasText value (node : HtmlNode) = 
    node.InnerText = value

let createDoc html =
    let doc = new HtmlDocument()
    doc.LoadHtml html
    doc.DocumentNode

open System.Text.RegularExpressions

// reduce size of bug reports
let cleanHtml (str:string) = 
    let replace pattern (replacement:string) str = Regex.Replace(str, pattern, replacement)
    str.Replace("\r", null).Replace("\n", null).Trim()
    |> replace ">\s*<" "><"
    |> replace "<head>.+?</head>" ""
    |> replace "<script[^>]*>.+?</script>" ""
    |> replace "<noscript>.+?</noscript>" ""

