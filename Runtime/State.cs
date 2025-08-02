
namespace Flexy.GameFlow
{
	public abstract class State : APropertyBindableBehaviour
	{
		[SerializeField] FlexyEvent		_showing;
		[SerializeField] FlexyEvent		_hiding;

		private		AssetRef<State>		_prefabRef;
		private		Int32				_uid;
		internal	FlowNode			_node;
		
		public		Int32				Uid			=> _uid;
		public		FlowNode			Node		=> _node;
		public		Object				OpenParams	=> _node.OpenParams;
		public		StateHandle			Handle		=> new(_uid, _node);
		public		AssetRef<State>		PrefabRef	=> _prefabRef;
		
		public		Boolean				IsOpened	=> _node?.IsOpened ?? false;
		public		Boolean				IsActive	=> _node?.IsActive ?? false;
		
		public		GameStage			GameStage	
		{
			get
			{
				for (var current = _node; current != null; current = current.Parent)
					if (current.State is GameStage gs)
						return gs;
						
				return null;
			}
		}
		
		protected internal virtual Boolean		TryGoBack				( )	=> true;
		protected internal virtual Transform	GetSubStatesContainer	( ) => null;
		
		internal			void	DoShow			( )	
		{ 
			try						{ OnShow( ); }
			catch ( Exception ex )	{ Debug.LogException( ex ); }
			
			if ( ReadyForBind )
				StateRebindAll ( );
			
			_showing.Raise( this );
		}
		internal			void	DoFwdHide		( )	
		{
			OnFwdHide( );
		}
		internal			void	DoBackShow		( )	
		{
			OnBackShow( );
		}
		internal			void	DoHide			( )	
		{
			try						{ OnHide( ); }
			catch ( Exception ex )	{ Debug.LogException( ex ); }
			
			_hiding.Raise( this );
			_node = null;
		}
		
		protected virtual	void	OnShow			( )	{ }
		protected virtual	void	OnFwdHide		( )	{ }
		protected virtual	void	OnBackShow		( )	{ }
		protected virtual	void	OnHide			( )	{ }
		
		internal			void	SetSelfRef		( AssetRef<State> stateRef )	=> _prefabRef = stateRef;
		internal			void	SetUid			( Int32 uid )					=> _uid	= uid;
		
		[Callable] public	void	Close			( )	
		{
			_node.Graph.RemoveNode( Handle );
		}

		public				void	StateRebindAll	( )	
		{
			foreach ( var bindableBehaviour in gameObject.GetComponentsInChildren<APropertyBindableBehaviour>( ) )
			{
				if ( bindableBehaviour.gameObject == gameObject )	bindableBehaviour.MakeBindReadyAndRebindAll( );
				else												bindableBehaviour.RebindAll( );				
			}
		}
		public override		String	ToString		( )	
		{
			return $"{gameObject.name} {(IsOpened?" opened":" closed")}{(OpenParams != null ? $" op:{OpenParams}" : "")}";
		}
		
		public record struct Opener( OpenCtx Ctx ) : IOpenerB
		{
			public	StateHandle	Open	( ) => Ctx.Open();
		}
	}
	
	public readonly record struct StateHandle( Int32 Uid, FlowNode Node )
	{
		internal	FlowNode	Node		{get;}			= Node; 
		internal	Int32		Uid			{get;}			= Uid;
		
		public 		Boolean 	IsValid		=> Node?.Uid == Uid;
		public		State		State		=> Node.State;

		public			StateHandle	Close		( ) => !IsValid ? default : Node.Close( );
		public override	String		ToString	( ) => $"StateHandle {Node.State.name} version:{Uid}";
	}
	
	[AttributeUsage(AttributeTargets.Method)]
	public class StateTestAttribute: Attribute {}
}