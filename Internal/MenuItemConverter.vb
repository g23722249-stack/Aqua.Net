Option Strict On
Option Explicit On

Imports System.ComponentModel
Imports System.ComponentModel.Design.Serialization
Imports System.Drawing
Imports System.Globalization

Namespace Global.Aqua

    ''' <summary>Lets the designer persist AquaMenu.Items as "New MenuItem(name, text, level, icon, selIcon)"
    ''' (see TextIconItemConverter for the same idea on ToolBar / Buttons items).</summary>
    Friend Class MenuItemConverter
        Inherits TypeConverter

        Public Overrides Function CanConvertTo(ByVal context As ITypeDescriptorContext, ByVal destinationType As Type) As Boolean
            Return destinationType Is GetType(InstanceDescriptor) OrElse MyBase.CanConvertTo(context, destinationType)
        End Function

        Public Overrides Function ConvertTo(ByVal context As ITypeDescriptorContext, ByVal culture As CultureInfo, ByVal value As Object, ByVal destinationType As Type) As Object
            Dim m As MenuItem = TryCast(value, MenuItem)
            If destinationType Is GetType(InstanceDescriptor) AndAlso m IsNot Nothing Then
                Dim ctor As Reflection.ConstructorInfo = GetType(MenuItem).GetConstructor(
                    New Type() {GetType(String), GetType(String), GetType(Integer), GetType(Image), GetType(Image)})
                Return New InstanceDescriptor(ctor, New Object() {m.Name, m.Text, Math.Max(1, m.Level), m.Icon, m.SelIcon}, True)
            End If
            Return MyBase.ConvertTo(context, culture, value, destinationType)
        End Function

    End Class

End Namespace
