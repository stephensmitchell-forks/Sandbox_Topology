﻿Imports System.Collections.Generic

Imports Grasshopper.Kernel
Imports Rhino.Geometry
Imports Grasshopper.Kernel.Types
Imports Grasshopper.Kernel.Data


Public Class TopologyPolygonEdge
    Inherits GH_Component
    ''' <summary>
    ''' Initializes a new instance of the TopologyPolygonEdge class.
    ''' </summary>
    Public Sub New()
        MyBase.New("Polygon Topology Edge", "Poly Topo Edge", _
     "Analyses the edge topology of a curve network consisting of closed polylines", _
     "Sandbox", "Topology")
    End Sub

    ''' <summary>
    ''' Registers all the input parameters for this component.
    ''' </summary>
    Protected Overrides Sub RegisterInputParams(ByVal pManager As GH_Component.GH_InputParamManager)
        pManager.AddCurveParameter("List of polylines", "C", "Network of closed polylines", GH_ParamAccess.list)
        pManager.AddNumberParameter("Tolerance", "T", "Tolerance value", GH_ParamAccess.item, 0.001)
    End Sub

    ''' <summary>
    ''' Registers all the output parameters for this component.
    ''' </summary>
    Protected Overrides Sub RegisterOutputParams(ByVal pManager As GH_Component.GH_OutputParamManager)
        pManager.AddLineParameter("List of edges", "E", "Ordered list of unique polyline edges", GH_ParamAccess.list)
        pManager.AddIntegerParameter("Loop-Edge structure", "LE", "For each polyline lists edge indices belonging to polyline", GH_ParamAccess.tree)
        pManager.AddIntegerParameter("Edge-Loop structure", "EL", "For each edge lists adjacent polyline indices", GH_ParamAccess.tree)
    End Sub

    ''' <summary>
    ''' This is the method that actually does the work.
    ''' </summary>
    ''' <param name="DA">The DA object is used to retrieve from inputs and store in outputs.</param>
    Protected Overrides Sub SolveInstance(DA As IGH_DataAccess)

        '1. Declare placeholder variables and assign initial invalid data.
        '   This way, if the input parameters fail to supply valid data, we know when to abort.
        Dim _C As New List(Of GH_Curve)
        Dim _T As Double = 0

        '2. Retrieve input data.
        If (Not DA.GetDataList(0, _C)) Then Return
        If (Not DA.GetData(1, _T)) Then Return

        '3. Abort on invalid inputs.
        If (Not _C.Count > 0) Then Return
        If (Not _T > 0) Then Return

        '4. Do something useful.
        Dim _polyList As New List(Of Polyline)

        '4.1. check inputs
        For Each _crv As GH_Curve In _C
            Dim _poly As Polyline = Nothing
            If Not _crv.Value.TryGetPolyline(_poly) Then Return
            _polyList.Add(_poly)
        Next

        '4.2. get topology
        Dim _edgeDict As Dictionary(Of String, Line) = getEdgeDict(_polyList, _T)
        Dim _fDict As Dictionary(Of String, List(Of String)) = getFaceDict(_polyList, _edgeDict, _T)
        Dim _edgeFaceDict As Dictionary(Of String, List(Of String)) = getEdgeFaceDict(_fDict, _edgeDict)

        '4.3: return results
        Dim _EValues As New List(Of Line)
        For Each _pair As KeyValuePair(Of String, Line) In _edgeDict
            _EValues.Add(_pair.Value)
        Next

        Dim _FEValues As New Grasshopper.DataTree(Of Int32)
        For Each _edgeIndexList As List(Of String) In _fDict.Values
            Dim _path As New GH_Path(_FEValues.BranchCount)
            For Each _item As String In _edgeIndexList
                _FEValues.Add(_item.Substring(1), _path)
            Next
        Next

        Dim _EFValues As New Grasshopper.DataTree(Of Int32)
        For Each _fList As List(Of String) In _edgeFaceDict.Values
            Dim _path As New GH_Path(_EFValues.BranchCount)
            For Each _item As String In _fList
                _EFValues.Add(_item.Substring(1), _path)
            Next
        Next

        DA.SetDataList(0, _EValues)
        DA.SetDataTree(1, _FEValues)
        DA.SetDataTree(2, _EFValues)

    End Sub

    Private Function getEdgeFaceDict(ByVal _fDict As Dictionary(Of String, List(Of String)), ByVal _edgeDict As Dictionary(Of String, Line)) As Dictionary(Of String, List(Of String))

        Dim _edgeFaceDict As New Dictionary(Of String, List(Of String))

        For Each _edgeID As String In _edgeDict.Keys

            Dim _fList As New List(Of String)

            For Each _key As String In _fDict.Keys
                Dim _values As List(Of String) = _fDict.Item(_key)

                For Each _value As String In _values
                    If _edgeID = _value Then
                        _fList.Add(_key)
                    End If
                Next

            Next

            _edgeFaceDict.Add(_edgeID, _fList)

        Next

        Return _edgeFaceDict

    End Function

    Private Function getFaceDict(ByVal _polyList As List(Of Polyline), ByVal _edgeDict As Dictionary(Of String, Line), ByVal _T As Double) As Dictionary(Of String, List(Of String))

        Dim _fDict As New Dictionary(Of String, List(Of String))

        Dim _count As Int32 = 0
        For Each _poly As Polyline In _polyList

            Dim _Fkey As String = "F" & _count

            Dim _edges As Line()
            _edges = _poly.GetSegments

            Dim _value As New List(Of String)

            For i As Int32 = 0 To UBound(_edges)

                For Each _key As String In _edgeDict.Keys
                    If compareEdges(_edgeDict.Item(_key), _edges(i), _T) Then
                        _value.Add(_key)
                        Exit For
                    End If
                Next

            Next

            _fDict.Add(_Fkey, _value)

            _count = _count + 1

        Next

        Return _fDict

    End Function

    Private Function getEdgeDict(ByVal _polyList As List(Of Polyline), ByVal _T As Double) As Dictionary(Of String, Line)

        Dim _edgeDict As New Dictionary(Of String, Line)

        Dim _count As Int32 = 0
        For Each _poly As Polyline In _polyList

            Dim _edges As Line()
            _edges = _poly.GetSegments

            For i As Int32 = 0 To UBound(_edges)

                ' check if edge exists in _edgeDict already
                If Not containsEdge(_edgeDict, _edges(i), _T) Then
                    Dim _key As String = "E" & _count
                    Dim _value As Line = _edges(i)
                    _edgeDict.Add(_key, _value)
                    _count = _count + 1
                End If

            Next

        Next

        Return _edgeDict

    End Function

    Private Function compareEdges(ByVal _line1 As Line, ByVal _line2 As Line, ByVal _T As Double) As Boolean

        Dim _startPt As Point3d = _line1.PointAt(0)
        Dim _endPt As Point3d = _line1.PointAt(1)
        If (_startPt.DistanceTo(_line2.PointAt(0)) < _T) And (_endPt.DistanceTo(_line2.PointAt(1)) < _T) Then
            'consider it the same edge
            Return True
        ElseIf (_startPt.DistanceTo(_line2.PointAt(1)) < _T) And (_endPt.DistanceTo(_line2.PointAt(0)) < _T) Then
            'consider it the same edge
            Return True
        End If

        Return False

    End Function

    Private Function containsEdge(ByVal _edgeDict As Dictionary(Of String, Line), ByVal _check As Line, ByVal _T As Double) As Boolean

        For Each _l As Line In _edgeDict.Values
            Dim _startPt As Point3d = _l.PointAt(0)
            Dim _endPt As Point3d = _l.PointAt(1)
            If (_startPt.DistanceTo(_check.PointAt(0)) < _T) And (_endPt.DistanceTo(_check.PointAt(1)) < _T) Then
                'consider it the same edge
                Return True
            ElseIf (_startPt.DistanceTo(_check.PointAt(1)) < _T) And (_endPt.DistanceTo(_check.PointAt(0)) < _T) Then
                'consider it the same edge
                Return True
            End If
        Next

        Return False

    End Function

    Public Overrides ReadOnly Property Exposure As Grasshopper.Kernel.GH_Exposure
        Get
            Return GH_Exposure.secondary
        End Get
    End Property

    ''' <summary>
    ''' Provides an Icon for every component that will be visible in the User Interface.
    ''' Icons need to be 24x24 pixels.
    ''' </summary>
    Protected Overrides ReadOnly Property Icon() As System.Drawing.Bitmap
        Get
            'You can add image files to your project resources and access them like this:
            Return My.Resources.TopologyPolyEdge
        End Get
    End Property

    ''' <summary>
    ''' Gets the unique ID for this component. Do not change this ID after release.
    ''' </summary>
    Public Overrides ReadOnly Property ComponentGuid() As Guid
        Get
            Return New Guid("{83e3815d-02aa-422c-9650-4905ff46d48a}")
        End Get
    End Property
End Class