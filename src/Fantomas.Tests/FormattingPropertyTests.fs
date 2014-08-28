﻿module Fantomas.Tests.FormattingPropertyTests

open NUnit.Framework
open System
open Fantomas.CodeFormatter
open Fantomas.FormatConfig
open FsCheck
open FsUnit
open Microsoft.FSharp.Compiler.Ast
open Microsoft.FSharp.Compiler.Range

let formatConfig = { FormatConfig.Default with StrictMode = true }

let generateSynMeasure =
    Gen.constant SynMeasure.One

let generateRange =
    Gen.constant range.Zero

let zero = range.Zero

let generateBasicConst _ =
    Gen.oneof 
        [ 
            Gen.constant SynConst.Unit
            Gen.map SynConst.Bool Arb.generate<_>
            Gen.map SynConst.SByte Arb.generate<_>
            Gen.map SynConst.Byte Arb.generate<_>
            Gen.map SynConst.Int16 Arb.generate<_>
            Gen.map SynConst.UInt16 Arb.generate<_>
            Gen.map SynConst.Int32 Arb.generate<_>
            Gen.map SynConst.UInt32 Arb.generate<_>
            Gen.map SynConst.Int64 Arb.generate<_>
            Gen.map SynConst.UInt64 Arb.generate<_>
            Gen.map SynConst.IntPtr Arb.generate<_>
            Gen.map SynConst.UIntPtr Arb.generate<_>
            Gen.map SynConst.Single Arb.generate<_>
            Gen.map SynConst.Double Arb.generate<_>
            Gen.map SynConst.Char Arb.generate<_>
            Gen.map SynConst.Decimal Arb.generate<_>
            Gen.map SynConst.UserNum Arb.generate<_>
            Gen.map (fun x -> SynConst.String(x, zero)) Arb.generate<_>
            Gen.map (fun x -> SynConst.Bytes(x, zero)) Arb.generate<_>
            Gen.map SynConst.UInt16s Arb.generate<_>
        ]

/// Constant is not really recursive.
/// Measure constant is only one-level deep.
let generateSynConst size =
    let genBasicConst = generateBasicConst size
    Gen.oneof 
        [ 
            Gen.constant SynConst.Unit
            Gen.map SynConst.Bool Arb.generate<_>
            Gen.map SynConst.SByte Arb.generate<_>
            Gen.map SynConst.Byte Arb.generate<_>
            Gen.map SynConst.Int16 Arb.generate<_>
            Gen.map SynConst.UInt16 Arb.generate<_>
            Gen.map SynConst.Int32 Arb.generate<_>
            Gen.map SynConst.UInt32 Arb.generate<_>
            Gen.map SynConst.Int64 Arb.generate<_>
            Gen.map SynConst.UInt64 Arb.generate<_>
            Gen.map SynConst.IntPtr Arb.generate<_>
            Gen.map SynConst.UIntPtr Arb.generate<_>
            Gen.map SynConst.Single Arb.generate<_>
            Gen.map SynConst.Double Arb.generate<_>
            Gen.map SynConst.Char Arb.generate<_>
            Gen.map SynConst.Decimal Arb.generate<_>
            Gen.map SynConst.UserNum Arb.generate<_>
            Gen.map (fun x -> SynConst.String(x, zero)) Arb.generate<_>
            Gen.map (fun x -> SynConst.Bytes(x, zero)) Arb.generate<_>
            Gen.map SynConst.UInt16s Arb.generate<_>
            Gen.map2 (fun x y -> SynConst.Measure(x, y)) genBasicConst generateSynMeasure
        ]

let alphaFreqList =
    [ 
        (26, Gen.elements <| seq {'a'..'z'});
        (26, Gen.elements <| seq {'A'..'Z'});
        (1, Gen.elements <| seq ['_'])
    ]

let digitFreqList = [ (10, Gen.elements <| seq {'0'..'9'}) ]

let letter = Gen.frequency alphaFreqList
let letterOrDigit = Gen.frequency <| alphaFreqList @ digitFreqList

let generateIdent size =
    (letter, Gen.listOfLength size letterOrDigit)
    ||> Gen.map2 (fun c cs -> String(c::cs |> List.toArray))

let generateLongIdentWithDots size =
    let genSubIdent = generateIdent (size/2)
    Gen.map (fun s -> LongIdentWithDots([Ident(s, zero)], [zero])) genSubIdent

let generateSynType size =
    let genSubLongIdentWithDots = generateLongIdentWithDots (size/2)
    Gen.oneof 
        [ 
            Gen.map SynType.LongIdent genSubLongIdentWithDots
        ]

let rec generateSynPat size = 
    let genSubLongIdentWithDots = generateLongIdentWithDots (size/2)
    if size <= 2 then
        let genConstructorArgs = (Gen.constant (SynConstructorArgs.Pats []))
        Gen.map2 (fun ident args -> SynPat.LongIdent(ident, None, None, args, None, zero)) genSubLongIdentWithDots genConstructorArgs
    else
        let genConstructorArgs = Gen.map SynConstructorArgs.Pats (Gen.listOf (generateSynPat (size/2)))
        Gen.oneof 
            [ 
                Gen.constant (SynPat.Wild zero)
                Gen.map2 (fun ident args -> SynPat.LongIdent(ident, None, None, args, None, zero)) genSubLongIdentWithDots genConstructorArgs
            ]

and generateSynSimplePats size =
    let genSubSynPat = generateSynPat (size/2)
    Gen.map (fun pat -> fst <| SimplePatsOfPat (SynArgNameGenerator()) pat) genSubSynPat

and generateSynMatchClause size =
    let genSubSynPat = generateSynPat (size/2)
    let genSubSynExpr = generateSynExpr (size/2)
    Gen.oneof
        [
            Gen.map2 (fun pat expr -> SynMatchClause.Clause(pat, None, expr, zero, SequencePointAtTarget)) genSubSynPat genSubSynExpr
            Gen.map3 (fun pat expr1 expr2 -> SynMatchClause.Clause(pat, Some expr1, expr2, zero, SequencePointAtTarget)) genSubSynPat genSubSynExpr genSubSynExpr
        ]

and generateSynBinding size =
    let genSubSynExpr = generateSynExpr (size/2)
    let genSubSynPat = generateSynPat (size/2)
    Gen.map2 (fun expr pat -> SynBinding.Binding(None, SynBindingKind.NormalBinding, false, false, [], PreXmlDoc.Empty, SynInfo.emptySynValData, pat, None, expr, zero, NoSequencePointAtLetBinding)) 
        genSubSynExpr genSubSynPat

and generateIdentExpr size =
    let genSubIdent = generateIdent (size/2)
    Gen.map (fun s -> SynExpr.Ident(Ident(s, zero))) genSubIdent

/// Complex expressions are only nested for function definitions and some control-flow constructs.
and generateSynExpr size =
    if size <= 2 then
        generateIdentExpr size
    else
        let genSubSynExpr = generateSynExpr (size/2)
        let genSubBasicExpr = generateBasicExpr (size/2)
        let genSubBasicExprList = Gen.listOf genSubBasicExpr
        let genSubIdentExpr = generateIdentExpr (size/2)
        let genSubSynType = generateSynType (size/2)
        let genSubSynTypeList = Gen.listOf genSubSynType
        let genSubSynSimplePats = generateSynSimplePats (size/2)
        let genSubSynMatchClauseList = Gen.listOf (generateSynMatchClause (size/2))
        let genSubSynPat = generateSynPat (size/2)
        let genSubIdent = generateIdent (size/2)
        let genSubLongIdentWithDots = generateLongIdentWithDots (size/2)
        let genSubSynConst = generateSynConst (size/2)
        let generateSynBindingList = Gen.listOf (generateSynBinding (size/2))
        Gen.frequency 
            [ 
                1, Gen.map (fun c -> SynExpr.Const(c, zero)) genSubSynConst
                1, Gen.map2 (fun expr typ -> SynExpr.Typed(expr, typ, zero)) genSubBasicExpr genSubSynType
                2, Gen.map (fun exprs -> SynExpr.Tuple(exprs, exprs |> List.map (fun _ -> zero), zero)) genSubBasicExprList
                2, Gen.map2 (fun b exprs -> SynExpr.ArrayOrList(b, exprs, zero)) Arb.generate<_> genSubBasicExprList
                1, Gen.map3 (fun b typ expr -> SynExpr.New(b, typ, SynExpr.Paren(expr, zero, None, zero), zero)) Arb.generate<_> genSubSynType genSubBasicExpr
                1, Gen.map2 (fun expr1 expr2 -> SynExpr.While(NoSequencePointAtWhileLoop, expr1, expr2, zero)) genSubBasicExpr genSubBasicExpr
                1, Gen.map2 (fun b expr -> SynExpr.ArrayOrListOfSeqExpr(b, expr, zero)) Arb.generate<_> genSubBasicExpr
                1, Gen.map2 (fun b expr -> SynExpr.CompExpr(b, ref true, expr, zero)) Arb.generate<_> genSubBasicExpr
                1, Gen.map (fun expr -> SynExpr.Do(expr, zero)) genSubBasicExpr
                1, Gen.map (fun expr -> SynExpr.Assert(expr, zero)) genSubBasicExpr
                1, Gen.map (fun expr -> SynExpr.Paren(expr, zero, None, zero)) genSubBasicExpr
                1, genSubIdentExpr
                1, Gen.map2 (fun b expr -> SynExpr.AddressOf(b, expr, zero, zero)) Arb.generate<_> genSubIdentExpr
                1, Gen.constant (SynExpr.Null zero)
                1, Gen.map (fun expr -> SynExpr.InferredDowncast(expr, zero)) genSubIdentExpr
                1, Gen.map (fun expr -> SynExpr.InferredUpcast(expr, zero)) genSubIdentExpr
                1, Gen.map2 (fun expr typ -> SynExpr.Upcast(expr, typ, zero)) genSubIdentExpr genSubSynType
                1, Gen.map2 (fun expr typ -> SynExpr.Downcast(expr, typ, zero)) genSubIdentExpr genSubSynType
                1, Gen.map2 (fun expr typ -> SynExpr.TypeTest(expr, typ, zero)) genSubIdentExpr genSubSynType
                1, Gen.map2 (fun expr1 expr2 -> SynExpr.DotIndexedGet(expr1, [SynIndexerArg.One expr2], zero, zero)) genSubBasicExpr genSubBasicExpr
                1, Gen.map3 (fun expr1 expr2 expr3 -> SynExpr.DotIndexedSet(expr1, [SynIndexerArg.One expr3], expr2, zero, zero, zero)) genSubBasicExpr genSubBasicExpr genSubBasicExpr
                1, Gen.map2 (fun expr longIdent -> SynExpr.DotGet(expr, zero, longIdent, zero)) genSubBasicExpr genSubLongIdentWithDots
                1, Gen.map3 (fun expr1 expr2 longIdent -> SynExpr.DotSet(expr1, longIdent, expr2, zero)) genSubBasicExpr genSubBasicExpr genSubLongIdentWithDots
                1, Gen.map2 (fun expr longIdent -> SynExpr.LongIdentSet(longIdent, expr, zero)) genSubBasicExpr genSubLongIdentWithDots
                1, Gen.map2 (fun b longIdent -> SynExpr.LongIdent(b, longIdent, None, zero)) Arb.generate<_> genSubLongIdentWithDots
                2, Gen.map3 (fun expr1 expr2 expr3 -> SynExpr.IfThenElse(expr1, expr2, Some expr3, NoSequencePointAtDoBinding, false, zero, zero)) genSubBasicExpr genSubBasicExpr genSubBasicExpr
                2, Gen.map2 (fun expr1 expr2 -> SynExpr.Sequential(SequencePointsAtSeq, true, expr1, expr2, zero)) genSubBasicExpr genSubBasicExpr
                1, Gen.map (fun expr -> SynExpr.Lazy(expr, zero)) genSubBasicExpr
                1, Gen.map2 (fun expr1 expr2 -> SynExpr.TryFinally(expr1, expr2, zero, NoSequencePointAtTry, NoSequencePointAtFinally)) genSubSynExpr genSubSynExpr
                1, Gen.map2 (fun expr clauses -> SynExpr.TryWith(expr, zero, clauses, zero, zero, NoSequencePointAtTry, NoSequencePointAtWith)) genSubSynExpr genSubSynMatchClauseList
                1, Gen.map2 (fun expr typs -> SynExpr.TypeApp(expr, zero, typs, typs |> List.map (fun _ -> zero), None, zero, zero)) genSubBasicExpr genSubSynTypeList
                4, Gen.map3 (fun b expr1 expr2 -> SynExpr.App(ExprAtomicFlag.NonAtomic, b, expr1, expr2, zero)) Arb.generate<_> genSubBasicExpr genSubBasicExpr
                4, Gen.map2 (fun expr clauses -> SynExpr.Match(NoSequencePointAtDoBinding, expr, clauses, false, zero)) genSubSynExpr genSubSynMatchClauseList
                2, Gen.map2 (fun b clauses -> SynExpr.MatchLambda(b, zero, clauses, NoSequencePointAtDoBinding, zero)) Arb.generate<_> genSubSynMatchClauseList
                2, Gen.map3 (fun b pat expr -> SynExpr.Lambda(b, false, pat, expr, zero)) Arb.generate<_> genSubSynSimplePats genSubSynExpr
                2, Gen.map5 (fun b expr1 expr2 expr3 s -> SynExpr.For(NoSequencePointAtForLoop, Ident(s, zero), expr1, b, expr2, expr3, zero)) Arb.generate<_> genSubBasicExpr genSubBasicExpr genSubBasicExpr genSubIdent
                2, Gen.map4 (fun b1 expr1 expr2 pat -> SynExpr.ForEach(NoSequencePointAtForLoop, SeqExprOnly false, b1, pat, expr1, expr2, zero)) Arb.generate<_> genSubBasicExpr genSubBasicExpr genSubSynPat
                8, Gen.map3 (fun b bindings expr -> SynExpr.LetOrUse(b, false, bindings, expr, zero)) Arb.generate<_> generateSynBindingList genSubSynExpr
            ]

and generateBasicExpr size =
    if size <= 2 then
        generateIdentExpr size
    else
        let genSubBasicExpr = generateBasicExpr (size/2)
        let genSubBasicExprList = Gen.listOf genSubBasicExpr
        let genSubIdentExpr = generateIdentExpr (size/2)
        let genSubSynType = generateSynType (size/2)
        let genSubSynTypeList = Gen.listOf genSubSynType
        let genSubSynPat = generateSynPat (size/2)
        let genSubIdent = generateIdent (size/2)
        let genSubLongIdentWithDots = generateLongIdentWithDots (size/2)
        let genSubSynConst = generateSynConst (size/2)
        Gen.frequency 
            [ 
                1, Gen.map (fun c -> SynExpr.Const(c, zero)) genSubSynConst
                1, Gen.map2 (fun expr typ -> SynExpr.Typed(expr, typ, zero)) genSubBasicExpr genSubSynType
                2, Gen.map (fun exprs -> SynExpr.Tuple(exprs, exprs |> List.map (fun _ -> zero), zero)) genSubBasicExprList
                2, Gen.map2 (fun b exprs -> SynExpr.ArrayOrList(b, exprs, zero)) Arb.generate<_> genSubBasicExprList
                1, Gen.map3 (fun b typ expr -> SynExpr.New(b, typ, SynExpr.Paren(expr, zero, None, zero), zero)) Arb.generate<_> genSubSynType genSubBasicExpr
                1, Gen.map2 (fun expr1 expr2 -> SynExpr.While(NoSequencePointAtWhileLoop, expr1, expr2, zero)) genSubBasicExpr genSubBasicExpr
                1, Gen.map2 (fun b expr -> SynExpr.ArrayOrListOfSeqExpr(b, expr, zero)) Arb.generate<_> genSubBasicExpr
                1, Gen.map2 (fun b expr -> SynExpr.CompExpr(b, ref true, expr, zero)) Arb.generate<_> genSubBasicExpr
                1, Gen.map (fun expr -> SynExpr.Do(expr, zero)) genSubBasicExpr
                1, Gen.map (fun expr -> SynExpr.Assert(expr, zero)) genSubBasicExpr
                1, Gen.map (fun expr -> SynExpr.Paren(expr, zero, None, zero)) genSubBasicExpr
                1, genSubIdentExpr
                1, Gen.map2 (fun b expr -> SynExpr.AddressOf(b, expr, zero, zero)) Arb.generate<_> genSubIdentExpr
                1, Gen.constant (SynExpr.Null zero)
                1, Gen.map (fun expr -> SynExpr.InferredDowncast(expr, zero)) genSubIdentExpr
                1, Gen.map (fun expr -> SynExpr.InferredUpcast(expr, zero)) genSubIdentExpr
                1, Gen.map2 (fun expr typ -> SynExpr.Upcast(expr, typ, zero)) genSubIdentExpr genSubSynType
                1, Gen.map2 (fun expr typ -> SynExpr.Downcast(expr, typ, zero)) genSubIdentExpr genSubSynType
                1, Gen.map2 (fun expr typ -> SynExpr.TypeTest(expr, typ, zero)) genSubIdentExpr genSubSynType
                1, Gen.map2 (fun expr1 expr2 -> SynExpr.DotIndexedGet(expr1, [SynIndexerArg.One expr2], zero, zero)) genSubBasicExpr genSubBasicExpr
                1, Gen.map3 (fun expr1 expr2 expr3 -> SynExpr.DotIndexedSet(expr1, [SynIndexerArg.One expr3], expr2, zero, zero, zero)) genSubBasicExpr genSubBasicExpr genSubBasicExpr
                1, Gen.map2 (fun expr longIdent -> SynExpr.DotGet(expr, zero, longIdent, zero)) genSubBasicExpr genSubLongIdentWithDots
                1, Gen.map3 (fun expr1 expr2 longIdent -> SynExpr.DotSet(expr1, longIdent, expr2, zero)) genSubBasicExpr genSubBasicExpr genSubLongIdentWithDots
                1, Gen.map2 (fun expr longIdent -> SynExpr.LongIdentSet(longIdent, expr, zero)) genSubBasicExpr genSubLongIdentWithDots
                1, Gen.map2 (fun b longIdent -> SynExpr.LongIdent(b, longIdent, None, zero)) Arb.generate<_> genSubLongIdentWithDots
                2, Gen.map2 (fun expr1 expr2 -> SynExpr.Sequential(SequencePointsAtSeq, true, expr1, expr2, zero)) genSubBasicExpr genSubBasicExpr
                1, Gen.map (fun expr -> SynExpr.Lazy(expr, zero)) genSubBasicExpr
                1, Gen.map2 (fun expr typs -> SynExpr.TypeApp(expr, zero, typs, typs |> List.map (fun _ -> zero), None, zero, zero)) genSubBasicExpr genSubSynTypeList
                4, Gen.map3 (fun b expr1 expr2 -> SynExpr.App(ExprAtomicFlag.NonAtomic, b, expr1, expr2, zero)) Arb.generate<_> genSubBasicExpr genSubBasicExpr
                2, Gen.map5 (fun b expr1 expr2 expr3 s -> SynExpr.For(NoSequencePointAtForLoop, Ident(s, zero), expr1, b, expr2, expr3, zero)) Arb.generate<_> genSubBasicExpr genSubBasicExpr genSubBasicExpr genSubIdent
                2, Gen.map5 (fun b1 b2 expr1 expr2 pat -> SynExpr.ForEach(NoSequencePointAtForLoop, SeqExprOnly b1, b2, pat, expr1, expr2, zero)) Arb.generate<_> Arb.generate<_> genSubBasicExpr genSubBasicExpr genSubSynPat
            ]
    
let generateParsedInput =
    let generateAST expr =
        let ident = Ident("Tmp", zero)
        ParsedInput.ImplFile
            (ParsedImplFileInput
               ("/tmp.fs", false,
                QualifiedNameOfFile ident, [], [],
                [SynModuleOrNamespace
                   ([ident], true,
                    [SynModuleDecl.DoExpr(NoSequencePointAtDoBinding, expr, zero)], PreXmlDocEmpty, [], None,
                    zero)], false))
    Gen.sized <| fun size -> Gen.map generateAST (generateSynExpr size)

type Input = Input of string

let tryFormatAST ast sourceCode config =
    try
        formatAST ast sourceCode config
    with _ ->
        ""

let generateInput = 
    Gen.map (fun ast -> Input (tryFormatAST ast None formatConfig)) generateParsedInput

let fromSynExpr expr =
    let ast =
        let ident = Ident("Tmp", zero)
        ParsedInput.ImplFile
            (ParsedImplFileInput
               ("/tmp.fs", false,
                QualifiedNameOfFile ident, [], [],
                [SynModuleOrNamespace
                   ([ident], true,
                    [SynModuleDecl.DoExpr(NoSequencePointAtDoBinding, expr, zero)], PreXmlDocEmpty, [], None,
                    zero)], false))
    Input (tryFormatAST ast None formatConfig)

let toSynExpr (Input s) =
    match parse false s with
    | ParsedInput.ImplFile
        (ParsedImplFileInput
            ("/tmp.fs", false,
            QualifiedNameOfFile _, [], [],
            [SynModuleOrNamespace
                (_, true, exprs, _, _, _, _)], false)) -> 
                List.tryPick(function (SynModuleDecl.DoExpr(_, expr, _)) -> Some expr | _ -> None) exprs
    | ast -> 
        //stdout.WriteLine("Can't convert {0}", sprintf "%A" ast)
        None

let rec shrinkSynExpr = function
    | SynExpr.LongIdentSet(_, expr, _)
    | SynExpr.DotIndexedGet(expr, _, _, _)
    | SynExpr.DotGet(expr, _, _, _)
    | SynExpr.DotSet(expr, _, _, _)
    | SynExpr.TypeTest(expr, _, _)
    | SynExpr.Upcast(expr, _, _)
    | SynExpr.Downcast(expr, _, _)
    | SynExpr.InferredUpcast(expr, _)
    | SynExpr.InferredDowncast(expr, _) 
    | SynExpr.Lazy(expr, _)
    | SynExpr.TypeApp(expr, _, _, _, _, _, _)
    | SynExpr.Do(expr, _)
    | SynExpr.Assert(expr, _)
    | SynExpr.Lambda(_, _, _, expr, _)
    | SynExpr.CompExpr(_, _, expr, _)
    | SynExpr.ArrayOrListOfSeqExpr(_, expr, _)
    | SynExpr.New(_, _, expr, _)
    | SynExpr.Typed(expr, _, _)
    | SynExpr.Paren(expr, _, _, _)
    | SynExpr.AddressOf(_, expr, _, _) -> 
        collectSynExpr expr
    
    | SynExpr.IfThenElse(expr1, expr2, None, _, _, _, _)
    | SynExpr.DotIndexedSet(expr1, _, expr2, _, _, _)
    | SynExpr.NamedIndexedPropertySet(_, expr1, expr2, _)
    | SynExpr.Sequential(_, _, expr1, expr2, _)
    | SynExpr.TryFinally(expr1, expr2, _, _, _)
    | SynExpr.App(_, _, expr1, expr2, _)
    | SynExpr.ForEach(_, _, _, _, expr1, expr2, _)
    | SynExpr.While(_, expr1, expr2, _)
    | SynExpr.Quote(expr1, _, expr2, _, _) -> 
        seq { yield! collectSynExpr expr1; yield! collectSynExpr expr2 }
    | SynExpr.Const(_, _) -> Seq.empty
    | SynExpr.ArrayOrList(_, exprs, _)
    | SynExpr.Tuple(exprs, _, _) ->
        seq { yield! Seq.collect collectSynExpr exprs }
    | SynExpr.Record(_, _, _, _) -> Seq.empty
    | SynExpr.ObjExpr(_, _, _, _, _, _) -> Seq.empty

    | SynExpr.IfThenElse(expr1, expr2, Some expr3, _, _, _, _)
    | SynExpr.DotNamedIndexedPropertySet(expr1, _, expr2, expr3, _)
    | SynExpr.For(_, _, expr1, _, expr2, expr3, _) -> 
        seq { yield! collectSynExpr expr1; yield! collectSynExpr expr2; yield! collectSynExpr expr3 }
    | SynExpr.MatchLambda(_, _, clauses, _, _) -> 
        seq { yield! Seq.collect collectSynMatchClause clauses }
    | SynExpr.TryWith(expr, _, clauses, _, _, _, _)
    | SynExpr.Match(_, expr, clauses, _, _) -> 
        seq { yield! collectSynExpr expr; yield! Seq.collect collectSynMatchClause clauses }
    | SynExpr.LetOrUse(_, _, bindings, expr, _) -> 
        seq { yield! Seq.collect collectSynBinding bindings; yield! collectSynExpr expr }
    | SynExpr.Ident(_) -> Seq.empty
    | SynExpr.LongIdent(_, _, _, _) -> Seq.empty
    | SynExpr.Null(_) -> Seq.empty
    | SynExpr.TraitCall(_, _, _, _) -> Seq.empty
    | SynExpr.JoinIn(_, _, _, _) -> Seq.empty
    | SynExpr.ImplicitZero(_) -> Seq.empty
    | SynExpr.YieldOrReturn(_, _, _) -> Seq.empty
    | SynExpr.YieldOrReturnFrom(_, _, _) -> Seq.empty
    | SynExpr.LetOrUseBang(_, _, _, _, _, _, _) -> Seq.empty
    | SynExpr.DoBang(_, _) -> Seq.empty
    | SynExpr.LibraryOnlyILAssembly(_, _, _, _, _) -> Seq.empty
    | SynExpr.LibraryOnlyStaticOptimization(_, _, _, _) -> Seq.empty
    | SynExpr.LibraryOnlyUnionCaseFieldGet(_, _, _, _) -> Seq.empty
    | SynExpr.LibraryOnlyUnionCaseFieldSet(_, _, _, _, _) -> Seq.empty
    | SynExpr.ArbitraryAfterError(_, _) -> Seq.empty
    | SynExpr.FromParseError(_, _) -> Seq.empty
    | SynExpr.DiscardAfterMissingQualificationAfterDot(_, _) -> Seq.empty

and collectSynExpr expr =
    seq { yield expr 
          yield! shrinkSynExpr expr }

and collectSynMatchClause (SynMatchClause.Clause(_, exprOpt, expr, _, _)) =
    seq { yield! exprOpt |> Option.map collectSynExpr |> fun arg -> defaultArg arg Seq.empty
          yield! collectSynExpr expr }

and collectSynBinding (SynBinding.Binding(_, _, _, _, _, _, _, _, _, expr, _, _)) =
    collectSynExpr expr

let shrinkInput input = 
    match toSynExpr input with
    | None -> 
        stdout.WriteLine("Can't shrink {0}", sprintf "%A" input)
        Seq.empty
    | Some expr -> 
        shrinkSynExpr expr |> Seq.map fromSynExpr

type Generators = 
    static member range() =
        Arb.fromGen generateRange
    static member Input() = 
        Arb.fromGenShrink (generateInput, shrinkInput)
        // Temporarily filter out bad generated inputs
        |> Arb.filter (fun input -> (toSynExpr input).IsSome)

[<TestFixtureSetUp>]
let registerFsCheckGenerators() =
    Arb.register<Generators>() |> ignore

/// An FsCheck runner which reports FsCheck test results to NUnit.
type private NUnitRunner () =
    interface IRunner with
        member __.OnStartFixture _ = ()
        member __.OnArguments (ntest, args, every) = 
            stdout.Write(every ntest args)
        member __.OnShrink(args, everyShrink) = 
            stdout.Write(everyShrink args)
        member __.OnFinished (name, result) =
            match result with
            | TestResult.True _data ->
                // TODO : Log the result data.
                Runner.onFinishedToString name result
                |> stdout.WriteLine

            | TestResult.Exhausted _data ->
                // TODO : Log the result data.
                Runner.onFinishedToString name result
                |> Assert.Inconclusive

            | TestResult.False (_,_,_,_,_) ->
                // TODO : Log more information about the test failure.
                Runner.onFinishedToString name result
                |> Assert.Fail

let private verboseConf = 
    {
        Config.Verbose with
            MaxTest = 200
            EndSize = 20
            Runner = NUnitRunner ()
    }

let tryFormatSourceString isFsi sourceCode config =
    try
        if sourceCode = null then sourceCode
        else formatSourceString isFsi sourceCode config
    with _ ->
        sourceCode

[<Test; Ignore>]
let ``running formatting twice should produce the same results``() =    
    Check.One(verboseConf,
        fun (Input sourceCode) ->
            let formatted = tryFormatSourceString false sourceCode formatConfig
            tryFormatSourceString false formatted formatConfig = formatted)

[<Test; Ignore>]
let ``should be able to convert inputs to SynExpr``() =    
    "x" |> Input |> toSynExpr |> fun opt -> opt.IsSome |> should equal true
    """let rec W = M
and b = V
and K = a
for jf = d downto p do
    u""" |> Input |> toSynExpr |> fun opt -> opt.IsSome |> should equal true

[<Test; Ignore>]
let ``should be able to shrink inputs``() =    
    "fun x -> x" |> Input |> shrinkInput |> Seq.map (fun (Input x) -> x.TrimEnd('\r', '\n')) |> Seq.toArray |> should equal [|"x"|]
    """let rec W = M
    u""" |> Input |> shrinkInput |> Seq.map (fun (Input x) -> x.TrimEnd('\r', '\n')) |> Seq.toArray |> should equal [|"M"; "u"|]



         

