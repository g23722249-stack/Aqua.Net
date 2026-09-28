Imports System.Globalization
Imports System.IO
Imports System.Text
Imports System.Text.RegularExpressions

''' <summary>One "BeginProperty Name ... EndProperty" block (Font, Images, ListImage1, ColumnHeader(1) ...).</summary>
Public Class VbBlock
    Public Property Name As String
    Public ReadOnly Property Props As New List(Of KeyValuePair(Of String, String))
    Public ReadOnly Property Blocks As New List(Of VbBlock)

    Public Function Prop(ByVal key As String) As String
        For Each kv In Props
            If String.Equals(kv.Key, key, StringComparison.OrdinalIgnoreCase) Then Return kv.Value
        Next
        Return Nothing
    End Function

    Public Function Block(ByVal key As String) As VbBlock
        For Each b In Blocks
            If String.Equals(b.Name, key, StringComparison.OrdinalIgnoreCase) Then Return b
        Next
        Return Nothing
    End Function
End Class

''' <summary>One "Begin Type Name ... End" control (or the form itself).</summary>
Public Class VbNode
    Inherits VbBlock
    Public Property TypeName As String            ' "Aqua.TextBox", "VB.Form" ...
    Public Property Index As Integer = -1          ' VB6 control-array index, -1 = not an array
    Public Property Parent As VbNode
    Public ReadOnly Property Children As New List(Of VbNode)
    Public Property Order As Integer               ' position in the .frm (z-order)

    Public ReadOnly Property ShortType As String
        Get
            Dim i As Integer = TypeName.LastIndexOf("."c)
            Return If(i >= 0, TypeName.Substring(i + 1), TypeName)
        End Get
    End Property

    Public Overrides Function ToString() As String
        Return TypeName & " " & Name & If(Index >= 0, "(" & Index & ")", "")
    End Function
End Class

''' <summary>Parser for the designer section of a VB6 .frm file.</summary>
Public NotInheritable Class FrmParser

    Private Shared ReadOnly BeginRx As New Regex("^\s*Begin\s+(\S+)\s+(\S+)")
    Private Shared ReadOnly BeginPropRx As New Regex("^\s*BeginProperty\s+([\w()]+)")
    Private Shared ReadOnly PropRx As New Regex("^\s*([\w.]+)\s*=\s*(.*)$")

    ''' <summary>Returns the VB.Form root node; VB_Name comes back through <paramref name="vbName"/>.</summary>
    Public Shared Function Parse(ByVal path As String, ByVal enc As Encoding, ByRef vbName As String) As VbNode
        Dim lines As String() = File.ReadAllLines(path, enc)
        Dim root As VbNode = Nothing
        Dim stack As New Stack(Of VbBlock)
        Dim order As Integer = 0
        vbName = Nothing
        For Each raw In lines
            If raw.StartsWith("Attribute VB_Name", StringComparison.Ordinal) Then
                Dim m = Regex.Match(raw, """(\w+)""")
                If m.Success Then vbName = m.Groups(1).Value
                Exit For
            End If
            Dim mb = BeginRx.Match(raw)
            If mb.Success Then
                Dim n As New VbNode With {.TypeName = mb.Groups(1).Value, .Name = mb.Groups(2).Value, .Order = order}
                order += 1
                If stack.Count = 0 Then
                    root = n
                Else
                    Dim p = TryCast(stack.Peek(), VbNode)
                    If p IsNot Nothing Then
                        n.Parent = p
                        p.Children.Add(n)
                    End If
                End If
                stack.Push(n)
                Continue For
            End If
            Dim mp = BeginPropRx.Match(raw)
            If mp.Success Then
                Dim b As New VbBlock With {.Name = mp.Groups(1).Value}
                If stack.Count > 0 Then stack.Peek().Blocks.Add(b)
                stack.Push(b)
                Continue For
            End If
            Dim t As String = raw.Trim()
            If t = "End" OrElse t = "EndProperty" Then
                If stack.Count > 0 Then stack.Pop()
                Continue For
            End If
            Dim mv = PropRx.Match(raw)
            If mv.Success AndAlso stack.Count > 0 Then
                Dim key As String = mv.Groups(1).Value
                Dim value As String = StripComment(mv.Groups(2).Value)
                Dim node = TryCast(stack.Peek(), VbNode)
                If node IsNot Nothing AndAlso key = "Index" Then
                    node.Index = CInt(Val(value))
                Else
                    stack.Peek().Props.Add(New KeyValuePair(Of String, String)(key, value))
                End If
            End If
        Next
        Return root
    End Function

    ''' <summary>Drops a trailing ' comment (e.g. "-1  'True") without touching quotes inside a string.</summary>
    Private Shared Function StripComment(ByVal v As String) As String
        Dim inStr As Boolean = False
        For i = 0 To v.Length - 1
            Dim c As Char = v(i)
            If c = """"c Then
                inStr = Not inStr
            ElseIf c = "'"c AndAlso Not inStr Then
                Return v.Substring(0, i).Trim()
            End If
        Next
        Return v.Trim()
    End Function

End Class

''' <summary>Helpers for raw VB6 property values.</summary>
Public Module VbValue

    Private ReadOnly FrxRx As New Regex("^""([^""]+\.frx)"":([0-9A-Fa-f]+)$", RegexOptions.IgnoreCase)

    Public Function IsString(ByVal raw As String) As Boolean
        Return raw IsNot Nothing AndAlso raw.StartsWith("""") AndAlso Not IsFrx(raw)
    End Function

    Public Function Str(ByVal raw As String) As String
        If raw Is Nothing Then Return ""
        Dim s As String = raw.Trim()
        If s.StartsWith("$""") Then s = s.Substring(1)
        If s.StartsWith("""") AndAlso s.EndsWith("""") AndAlso s.Length >= 2 Then s = s.Substring(1, s.Length - 2)
        Return s.Replace("""""", """")
    End Function

    Public Function Num(ByVal raw As String) As Double
        If raw Is Nothing Then Return 0
        Dim s As String = raw.Trim()
        If s.StartsWith("&H", StringComparison.OrdinalIgnoreCase) Then
            ' VB6 hex literals are 32-bit signed: &H80000005& is a system colour (negative), not 2147483653
            Dim hex As String = s.Substring(2).TrimEnd("&"c)
            Return If(hex.Length <= 8, CDbl(Convert.ToInt32(hex, 16)), CDbl(Convert.ToInt64(hex, 16)))
        End If
        Dim d As Double
        If Double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, d) Then Return d
        Return Val(s)
    End Function

    Public Function Bool(ByVal raw As String) As Boolean
        If raw Is Nothing Then Return False
        Dim s As String = raw.Trim()
        If s.Equals("True", StringComparison.OrdinalIgnoreCase) Then Return True
        If s.Equals("False", StringComparison.OrdinalIgnoreCase) Then Return False
        Return Num(s) <> 0
    End Function

    Public Function IsFrx(ByVal raw As String) As Boolean
        Return raw IsNot Nothing AndAlso FrxRx.IsMatch(raw.Trim())
    End Function

    Public Function Frx(ByVal raw As String, ByRef file As String, ByRef offset As Integer) As Boolean
        Dim m = FrxRx.Match(If(raw, "").Trim())
        If Not m.Success Then Return False
        file = m.Groups(1).Value
        offset = Convert.ToInt32(m.Groups(2).Value, 16)
        Return True
    End Function

End Module
