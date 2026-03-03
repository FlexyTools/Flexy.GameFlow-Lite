using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using Object = System.Object;

namespace Flexy.GameFlow.Editor
{
	[CustomPropertyDrawer(typeof(CrossSceneRef<>))]
	public class CrossSceneRefDrawer : PropertyDrawer
	{
		private Dictionary<(Hash128, Int64), Component>? _crossSceneRefs;
		
		public override		void	OnGUI				( Rect position, SerializedProperty property, GUIContent label )	
		{
			OnGUI( position, property, label, GetRefType( fieldInfo ) );
		}
		
		protected			void	OnGUI				( Rect position, SerializedProperty property, GUIContent label, Type type )
		{	
			label				= EditorGUI.BeginProperty( position, label, property );
			
			var sceneProp		= property.FindPropertyRelative( "_scene" );
			var uidProp			= property.FindPropertyRelative( "_uid" );
			
			if (_crossSceneRefs == null)
			{
				_crossSceneRefs = new Dictionary<(Hash128, Int64), Component>();
				var allrefs = UnityEngine.Object.FindObjectsByType<CrossSceneRef>(FindObjectsInactive.Include, FindObjectsSortMode.None);
				foreach (var gref in allrefs)
				{
					if (gref.gameObject.TryGetComponent(type, out var component))
					{
						var hash128 = Hash128.Parse( AssetDatabase.AssetPathToGUID(gref.gameObject.scene.path) );
						_crossSceneRefs.Add((hash128, gref.Uid), component);
					}
				}
			}
			
			var scene	= sceneProp.hash128Value;
			var uid		= uidProp.longValue;
			_crossSceneRefs.TryGetValue((scene, uid), out var beh);
			
			// String ref representation
			{
				GUILayout.BeginHorizontal();
				
				if (scene != default && uid != default && beh == null)
					GUILayout.Label($"missing", GUILayout.Width(position.x + EditorGUIUtility.labelWidth));
				else
					GUILayout.Space(position.x + EditorGUIUtility.labelWidth + 2);
					
				GUILayout.Label($"{Path.GetFileNameWithoutExtension( AssetDatabase.GUIDToAssetPath(scene.ToString()) )}[{uid}]");
				GUILayout.EndHorizontal();
			}
			
			EditorGUI.BeginChangeCheck( );
			var newobj		= EditorGUI.ObjectField( position, label, beh, type, true );
			var isChanged	= EditorGUI.EndChangeCheck( );
			
			if (isChanged)
			{
				var go = ((Component)newobj).gameObject;
				var targetComponent = go.GetComponent(type);
				
				if (targetComponent != null)
				{
					sceneProp	.hash128Value	= Hash128.Parse( AssetDatabase.AssetPathToGUID(go.scene.path) );
					
					if (go.TryGetComponent<CrossSceneRef>(out var csr))
						uidProp	.longValue	= csr.Uid;
					else
						Debug.LogWarning("Target Object dont have CrossSceneRef component! Add one.", go);
				}
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