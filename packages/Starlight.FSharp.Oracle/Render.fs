module Starlight.FSharp.Render

open Starlight.FSharp.RenderImpl

// ---------------------------------------------------------------------------
// Public types
// ---------------------------------------------------------------------------

type TocEntry = RenderImpl.TocEntry
type RenderedPage = RenderImpl.RenderedPage
type LinkResolver = RenderImpl.LinkResolver

// ---------------------------------------------------------------------------
// Public API
// ---------------------------------------------------------------------------

let renderEntityPage = Pages.renderEntityPage
let renderNamespacePage = Pages.renderNamespacePage
let renderModulePage = Pages.renderModulePage
let renderRootIndexPage = Pages.renderRootIndexPage
