using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using Object = System.Object;

namespace Flexy.GameFlow.Editor
{
	[CustomPropertyDrawer(typeof(GlobalRef<>))]
	public class GlobalRefDrawer : PropertyDrawer
	{
		// used to store cached objects of current SerializedObject our drawer part of
		private Dictionary<Hash128, Component>? _globalRefs;
		
		public override		void	OnGUI				( Rect position, SerializedProperty property, GUIContent label )	
		{
			OnGUI( position, property, label, GetRefType( fieldInfo ) );
		}
		
		protected			void	OnGUI				( Rect position, SerializedProperty property, GUIContent label, Type type )
		{	
			label				= EditorGUI.BeginProperty( position, label, property );
			
			var uidProp			= property.FindPropertyRelative( "_uid" );
			
			if (_globalRefs == null)
			{
				_globalRefs = new Dictionary<Hash128, Component>();
				var allrefs = UnityEngine.Object.FindObjectsByType<GlobalRef>(FindObjectsInactive.Include, FindObjectsSortMode.None);
				foreach (var gref in allrefs)
				{
					if (gref.gameObject.TryGetComponent(type, out var component))
						_globalRefs.Add(gref.Uid, component);
				}
			}
			
			var id = uidProp.hash128Value;
			_globalRefs.TryGetValue(id, out var beh);
			
			EditorGUI.BeginChangeCheck( );
			var newobj	= EditorGUI.ObjectField( position, label, beh, type, true );
			
			var isChanged = EditorGUI.EndChangeCheck( );
			
			if (isChanged)
			{
				var go = ((Component)newobj).gameObject;
				var targetComponent = go.GetComponent(type);
				
				if (targetComponent != null)
					uidProp.hash128Value = ((Component)newobj).GetComponent<GlobalRef>().Uid;
			}
			
			EditorGUI.EndProperty();
		}
		protected static	Type	GetRefType			( FieldInfo fieldInfo )												
		{
			var type = fieldInfo.FieldType;
			
			if			(type.IsArray)																type = fieldInfo.FieldType.GetElementType()!;
			else if		(type.IsGenericType && type.GetGenericTypeDefinition() == typeof(List<>))	type = fieldInfo.FieldType.GetGenericArguments()[0];
			
			if (type.IsGenericType)	type = type.GetGenericArguments()[0];
			else					type = typeof(Object);
			
			return type;
		}
	}
}