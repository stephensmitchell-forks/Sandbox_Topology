﻿Imports Rhino.Geometry

Public Class PointTopological

    Private _p As Point3d
    Private _i As Int32     'internal indexing of the points
    Private _l As List(Of PLineTopological) = Nothing

    Public Sub New(ByVal P As Point3d, ByVal I As Int32)

        _p = P
        _i = I
        '_l = L

    End Sub

    '##### PROPERTIES #####

    Public ReadOnly Property Point As Point3d
        Get
            Return _p
        End Get
    End Property

    Public ReadOnly Property Index As Int32
        Get
            Return _i
        End Get
    End Property

    Public Property PLines As List(Of PLineTopological)
        Set(ByVal value As List(Of PLineTopological))
            _l = value
        End Set
        Get
            Return _l
        End Get
    End Property

End Class
