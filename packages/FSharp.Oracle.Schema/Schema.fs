module FSharp.Oracle.Schema

// ---------------------------------------------------------------------------
// XML doc comment payloads
// ---------------------------------------------------------------------------

#nowarn 40

type XmlDocParam =
    {
        Name: string
        Doc: string
    }

type XmlDoc =
    {
        Summary: string option
        Remarks: string option
        Returns: string option
        Params: XmlDocParam list
        Examples: string list
    }

/// Whether an API is obsolete and, if so, whether a custom message was supplied.
[<RequireQualifiedAccess>]
type ObsoleteInfo =
    | Active
    | Deprecated
    | DeprecatedWithMessage of string

// ---------------------------------------------------------------------------
// Signature tokens
// ---------------------------------------------------------------------------

/// What a name in a declaration refers to, so the renderer can style it and point
/// it at the right anchor without the extractor knowing anything about HTML.
[<RequireQualifiedAccess>]
type DeclarationRole =
    /// A member of the enclosing type (method, property, record field, enum case).
    | Member
    /// A union case.
    | UnionCase
    /// A constructor, written `new`.
    | Constructor

/// A token stream describing a signature.
///
/// Deliberately free of presentation: no HTML, no URLs, no space counts. The renderer
/// decides what a keyword looks like, how wide an indent is, whether a type reference
/// becomes a link, and how columns line up. Keeping those decisions out of the
/// extractor is what lets the same IR drive something other than this Astro site.
[<RequireQualifiedAccess>]
type TextNode =
    /// Plain text, e.g. "unit", "string".
    | Text of string
    /// A reference to a named type. Whether it becomes a link is the renderer's call:
    /// only it knows which types have a page.
    | TypeRef of name: string * fullName: string
    /// A generic type variable, e.g. "T" (the tick is implied).
    | TypeVar of string
    /// The name of a parameter in a signature.
    | ParameterName of string
    /// An F# keyword, e.g. "val", "type", "member".
    | Keyword of string
    /// Punctuation, e.g. ":", "->", "*", "<". Escaping is the renderer's problem.
    | Punctuation of string
    /// The apostrophe introducing a type variable.
    | Tick
    /// An attribute written above a declaration, e.g. "[<Struct>]".
    | Attribute of string
    /// The name of a declaration on the current page, linking to its own entry.
    /// `text` and `anchor` differ for overloaded constructors, which all read `new`
    /// but anchor to `new`, `new-1`, ...
    | DeclarationName of text: string * anchor: string * role: DeclarationRole
    | Space
    | NewLine
    /// Structural indentation. The renderer decides how wide a level is.
    | Indent of levels: int
    /// A flat sequence of child tokens.
    | Node of nodes: TextNode list

// ---------------------------------------------------------------------------
// Parameters
// ---------------------------------------------------------------------------

type Parameter =
    {
        Name: string
        Type: TextNode
        /// `?name` in F#. The type is the unwrapped one: F# writes `?x: int`, not
        /// `?x: int option`.
        IsOptional: bool
    }

// ---------------------------------------------------------------------------
// Module-level functions and values
// ---------------------------------------------------------------------------

type Function =
    {
        Name: string
        FullName: string
        /// Curried parameter groups. Each outer list is a curried group;
        /// each inner list holds the parameters within that group (tupled parameters).
        Parameters: Parameter list list
        ReturnType: TextNode
        /// Generic parameters with constraints, e.g. `<'T when 'T : comparison>`.
        GenericParameters: TextNode option
        IsInline: bool
        IsMutable: bool
        /// `(|Integer|)`, `(|Even|_|)` and friends, which are documented separately.
        IsActivePattern: bool
        XmlDoc: XmlDoc
        ObsoleteInfo: ObsoleteInfo
    }

type Value =
    {
        Name: string
        FullName: string
        Type: TextNode
        /// Set for `[<Literal>]` bindings, whose value is part of their contract.
        LiteralValue: string option
        /// Generic parameters with constraints, e.g. `<'T when 'T : comparison>`.
        GenericParameters: TextNode option
        IsInline: bool
        IsMutable: bool
        XmlDoc: XmlDoc
        ObsoleteInfo: ObsoleteInfo
    }

// ---------------------------------------------------------------------------
// Entity members (methods, properties, constructors)
// ---------------------------------------------------------------------------

[<RequireQualifiedAccess>]
type MemberKind =
    | Method
    | Property
    | Constructor
    | Operator

type Member =
    {
        Kind: MemberKind
        Name: string
        FullName: string
        /// The .NET name, e.g. `op_Addition` for `(+)`. The display name of an operator
        /// is not usable as a URL fragment, so the renderer anchors on this instead.
        CompiledName: string
        /// Curried parameter groups. Each outer list is a curried group;
        /// each inner list holds the parameters within that group (tupled parameters).
        Parameters: Parameter list list
        /// The return type of the member (property type, method return type, or
        /// constructed type for constructors).
        ReturnType: TextNode
        /// Generic parameters with constraints, e.g. `<'T when 'T : comparison>`.
        GenericParameters: TextNode option
        XmlDoc: XmlDoc
        IsStatic: bool
        /// True for `abstract member` declarations (interface and abstract class members).
        IsAbstract: bool
        IsInline: bool
        /// Properties only. A property with neither is write-only.
        HasGetter: bool
        HasSetter: bool
        ObsoleteInfo: ObsoleteInfo
    }

// ---------------------------------------------------------------------------
// Record fields, enum cases, union cases
// ---------------------------------------------------------------------------

type Field =
    {
        Name: string
        Type: TextNode
        /// Set for enum cases, whose declaration reads `Name = value`.
        LiteralValue: string option
        XmlDoc: XmlDoc
    }

type UnionCase =
    {
        Name: string
        FullName: string
        Fields: Field list
        XmlDoc: XmlDoc
    }

// ---------------------------------------------------------------------------
// Entities (types)
// ---------------------------------------------------------------------------

type RecordEntity =
    {
        Name: string
        FullName: string
        XmlDoc: XmlDoc
        /// Attributes worth showing, already formatted, e.g. "[<RequireQualifiedAccess>]".
        Attributes: string list
        /// Interfaces the type declares, excluding those F# derives automatically.
        Interfaces: TextNode list
        /// The type's generic parameters with constraints, e.g. `<'T when 'T : comparison>`.
        GenericParameters: TextNode option
        Fields: Field list
        Members: Member list
        ObsoleteInfo: ObsoleteInfo
        IsStruct: bool
    }

type UnionEntity =
    {
        Name: string
        FullName: string
        XmlDoc: XmlDoc
        /// Attributes worth showing, already formatted, e.g. "[<RequireQualifiedAccess>]".
        Attributes: string list
        /// Interfaces the type declares, excluding those F# derives automatically.
        Interfaces: TextNode list
        GenericParameters: TextNode option
        Cases: UnionCase list
        Members: Member list
        ObsoleteInfo: ObsoleteInfo
        IsStruct: bool
    }

type AbbrevEntity =
    {
        Name: string
        FullName: string
        XmlDoc: XmlDoc
        /// Attributes worth showing, already formatted, e.g. "[<RequireQualifiedAccess>]".
        Attributes: string list
        GenericParameters: TextNode option
        /// The abbreviated type (right-hand side only).
        AbbreviatedType: TextNode
        ObsoleteInfo: ObsoleteInfo
        IsStruct: bool
    }

type ClassEntity =
    {
        Name: string
        FullName: string
        XmlDoc: XmlDoc
        /// Attributes worth showing, already formatted, e.g. "[<RequireQualifiedAccess>]".
        Attributes: string list
        GenericParameters: TextNode option
        /// The class this one inherits, when it is not `obj`.
        BaseType: TextNode option
        /// Interfaces the type declares. What a caller can pass it as.
        Interfaces: TextNode list
        Members: Member list
        ObsoleteInfo: ObsoleteInfo
        IsStruct: bool
    }

type InterfaceEntity =
    {
        Name: string
        FullName: string
        XmlDoc: XmlDoc
        /// Attributes worth showing, already formatted, e.g. "[<RequireQualifiedAccess>]".
        Attributes: string list
        GenericParameters: TextNode option
        /// Interfaces this one inherits.
        Interfaces: TextNode list
        Members: Member list
        ObsoleteInfo: ObsoleteInfo
        IsStruct: bool
    }

type EnumEntity =
    {
        Name: string
        FullName: string
        XmlDoc: XmlDoc
        /// Attributes worth showing, already formatted, e.g. "[<RequireQualifiedAccess>]".
        Attributes: string list
        GenericParameters: TextNode option
        Fields: Field list
        ObsoleteInfo: ObsoleteInfo
        IsStruct: bool
    }

type MeasureEntity =
    {
        Name: string
        FullName: string
        XmlDoc: XmlDoc
        /// Attributes worth showing, already formatted, e.g. "[<RequireQualifiedAccess>]".
        Attributes: string list
        GenericParameters: TextNode option
        ObsoleteInfo: ObsoleteInfo
    }

type ExceptionEntity =
    {
        Name: string
        FullName: string
        XmlDoc: XmlDoc
        /// Attributes worth showing, already formatted, e.g. "[<RequireQualifiedAccess>]".
        Attributes: string list
        Fields: Field list
        ObsoleteInfo: ObsoleteInfo
    }

type DelegateEntity =
    {
        Name: string
        FullName: string
        XmlDoc: XmlDoc
        /// Attributes worth showing, already formatted, e.g. "[<RequireQualifiedAccess>]".
        Attributes: string list
        GenericParameters: TextNode option
        /// Parameter types of the delegate's Invoke method.
        Parameters: TextNode list
        ReturnType: TextNode
        ObsoleteInfo: ObsoleteInfo
    }

[<RequireQualifiedAccess>]
type Entity =
    | Record of RecordEntity
    | Union of UnionEntity
    | Abbrev of AbbrevEntity
    | Class of ClassEntity
    | Interface of InterfaceEntity
    | Enum of EnumEntity
    | Measure of MeasureEntity
    | Exception of ExceptionEntity
    | Delegate of DelegateEntity

    member this.Name =
        match this with
        | Record e -> e.Name
        | Union e -> e.Name
        | Abbrev e -> e.Name
        | Class e -> e.Name
        | Interface e -> e.Name
        | Enum e -> e.Name
        | Measure e -> e.Name
        | Exception e -> e.Name
        | Delegate e -> e.Name

    member this.FullName =
        match this with
        | Record e -> e.FullName
        | Union e -> e.FullName
        | Abbrev e -> e.FullName
        | Class e -> e.FullName
        | Interface e -> e.FullName
        | Enum e -> e.FullName
        | Measure e -> e.FullName
        | Exception e -> e.FullName
        | Delegate e -> e.FullName

    member this.XmlDoc =
        match this with
        | Record e -> e.XmlDoc
        | Union e -> e.XmlDoc
        | Abbrev e -> e.XmlDoc
        | Class e -> e.XmlDoc
        | Interface e -> e.XmlDoc
        | Enum e -> e.XmlDoc
        | Measure e -> e.XmlDoc
        | Exception e -> e.XmlDoc
        | Delegate e -> e.XmlDoc

    member this.GenericParameters: TextNode option =
        match this with
        | Record e -> e.GenericParameters
        | Union e -> e.GenericParameters
        | Abbrev e -> e.GenericParameters
        | Class e -> e.GenericParameters
        | Interface e -> e.GenericParameters
        | Enum e -> e.GenericParameters
        | Measure e -> e.GenericParameters
        | Delegate e -> e.GenericParameters
        | Exception _ -> None

    member this.Attributes =
        match this with
        | Record e -> e.Attributes
        | Union e -> e.Attributes
        | Abbrev e -> e.Attributes
        | Class e -> e.Attributes
        | Interface e -> e.Attributes
        | Enum e -> e.Attributes
        | Measure e -> e.Attributes
        | Exception e -> e.Attributes
        | Delegate e -> e.Attributes

    member this.ObsoleteInfo =
        match this with
        | Record e -> e.ObsoleteInfo
        | Union e -> e.ObsoleteInfo
        | Abbrev e -> e.ObsoleteInfo
        | Class e -> e.ObsoleteInfo
        | Interface e -> e.ObsoleteInfo
        | Enum e -> e.ObsoleteInfo
        | Measure e -> e.ObsoleteInfo
        | Exception e -> e.ObsoleteInfo
        | Delegate e -> e.ObsoleteInfo

    member this.IsStruct =
        match this with
        | Record e -> e.IsStruct
        | Union e -> e.IsStruct
        | Abbrev e -> e.IsStruct
        | Class e -> e.IsStruct
        | Interface e -> e.IsStruct
        | Enum e -> e.IsStruct
        | Measure _ -> false
        | Exception _ -> false
        | Delegate _ -> false

// ---------------------------------------------------------------------------
// Modules
// ---------------------------------------------------------------------------

type Module =
    {
        Name: string
        FullName: string
        /// The parent namespace, e.g. "Reference.Geometry" for "Reference.Geometry.Points".
        /// Empty string for root-level modules with no namespace.
        Namespace: string
        XmlDoc: string option
        Entities: Entity list
        Functions: Function list
        Values: Value list
        /// True for synthetic modules that carry bare namespace-level types.
        /// The plugin generates individual entity pages from these rather than a
        /// module page, so these have no page of their own.
        IsSynthetic: bool
        ObsoleteInfo: ObsoleteInfo
    }

// ---------------------------------------------------------------------------
// Namespaces
// ---------------------------------------------------------------------------

type Namespace =
    {
        /// Short display name, e.g. "global", "Reference", "Geometry".
        Name: string
        /// Fully-qualified name, e.g. "", "Reference", "Reference.Geometry".
        FullName: string
    }

// ---------------------------------------------------------------------------
// Assemblies - top-level root
// ---------------------------------------------------------------------------

type Assembly =
    {
        Name: string
        Namespaces: Namespace list
        Modules: Module list
    }

type Root =
    {
        Assemblies: Assembly list
    }
