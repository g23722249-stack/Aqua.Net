Option Strict On
Option Explicit On

Imports System.Collections.ObjectModel
Imports System.ComponentModel
Imports System.ComponentModel.Design.Serialization
Imports System.Drawing
Imports System.Globalization

' Grid.Columns: the grid's column headers as a designer-editable list. VB6 persisted them as the
' Grid's HeaderText_i / HeaderWidth_i ... properties; the .NET port first added them in code
' (Grid.Header.AdditionHeader). Editing Columns in the designer rebuilds the header bar the same way.
Namespace Global.Aqua

    ''' <summary>One column header (VB6 AdditionHeader(icon, text, alignment, width, sort, order)).</summary>
    <TypeConverter(GetType(GridColumnConverter))>
    Public Class GridColumn

        Public Sub New()
        End Sub

        Public Sub New(ByVal text As String, ByVal width As Integer, ByVal alignment As AlignmentConstants,
                       ByVal sort As Boolean, ByVal sortOrder As Aqua.SortOrder, ByVal icon As Image)
            Me.Text = If(text, "")
            Me.Width = width
            Me.Alignment = alignment
            Me.Sort = sort
            Me.SortOrder = sortOrder
            Me.Icon = icon
        End Sub

        <Category("外觀")>
        Public Property Text As String = ""
        <Category("配置"), Description("寬度（像素）；0 = 預設 60。最後一欄會延伸到填滿。")>
        Public Property Width As Integer = 0
        <Category("外觀")>
        Public Property Alignment As AlignmentConstants = AlignmentConstants.LeftJustify
        <Category("行為"), Description("點標題時是否排序。")>
        Public Property Sort As Boolean = False
        <Category("行為")>
        Public Property SortOrder As Aqua.SortOrder = Aqua.SortOrder.None
        <Category("外觀")>
        Public Property Icon As Image

        Public Overrides Function ToString() As String
            Return If(Text.Trim() = "", "(空白)", Text) & "  " & If(Width > 0, Width.ToString(), "預設") & "px"
        End Function
    End Class

    ''' <summary>Grid.Columns; every change rebuilds the header bar.</summary>
    Public Class GridColumnCollection
        Inherits Collection(Of GridColumn)

        Private ReadOnly _owner As Grid

        Friend Sub New(ByVal owner As Grid)
            _owner = owner
        End Sub

        Protected Overrides Sub InsertItem(ByVal index As Integer, ByVal item As GridColumn)
            MyBase.InsertItem(index, If(item, New GridColumn()))
            _owner.ApplyColumns()
        End Sub

        Protected Overrides Sub RemoveItem(ByVal index As Integer)
            MyBase.RemoveItem(index)
            _owner.ApplyColumns()
        End Sub

        Protected Overrides Sub SetItem(ByVal index As Integer, ByVal item As GridColumn)
            MyBase.SetItem(index, If(item, New GridColumn()))
            _owner.ApplyColumns()
        End Sub

        Protected Overrides Sub ClearItems()
            MyBase.ClearItems()
            _owner.ApplyColumns()
        End Sub
    End Class

    ''' <summary>Writes a GridColumn as "New GridColumn(text, width, alignment, sort, order, icon)".</summary>
    Friend Class GridColumnConverter
        Inherits TypeConverter

        Public Overrides Function CanConvertTo(ByVal context As ITypeDescriptorContext, ByVal destinationType As Type) As Boolean
            Return destinationType Is GetType(InstanceDescriptor) OrElse MyBase.CanConvertTo(context, destinationType)
        End Function

        Public Overrides Function ConvertTo(ByVal context As ITypeDescriptorContext, ByVal culture As CultureInfo, ByVal value As Object, ByVal destinationType As Type) As Object
            Dim c As GridColumn = TryCast(value, GridColumn)
            If destinationType Is GetType(InstanceDescriptor) AndAlso c IsNot Nothing Then
                Dim ctor As Reflection.ConstructorInfo = GetType(GridColumn).GetConstructor(New Type() {
                    GetType(String), GetType(Integer), GetType(AlignmentConstants), GetType(Boolean), GetType(Aqua.SortOrder), GetType(Image)})
                Return New InstanceDescriptor(ctor, New Object() {c.Text, c.Width, c.Alignment, c.Sort, c.SortOrder, c.Icon}, True)
            End If
            Return MyBase.ConvertTo(context, culture, value, destinationType)
        End Function
    End Class

End Namespace
