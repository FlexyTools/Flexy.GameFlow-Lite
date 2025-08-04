
namespace Flexy.GameFlow
{
	public abstract class State : APropertyBindableBehaviour
	{
		[SerializeField] FlexyEvent		_showing;
		[SerializeField] FlexyEvent		_hiding;
		
		internal	FlowNode			_node;
		
		public		AssetRef<State>		PrefabRef	{get; internal set;}
		
		public		FlowNode			Node		=> _node;
		public		Object				OpenParams	=> _node.OpenParams;
		public		StateHandle			Handle		=> new(_node);
		
		public		Boolean				IsOpened	=> _node?.IsOpened ?? false;
		public		Boolean				IsShowed	=> _node?.IsShowed ?? false;
		
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
		
		protected internal virtual	Boolean		TryGoBack				( )	=> !_node.IsLocked;
		protected internal virtual	Transform	GetSubStatesContainer	( ) => null;
		
		internal			void	DoShow			( )	
		{ 
			try						{ OnShow( ); }
			catch ( Exception ex )	{ Debug.LogException( ex ); }
			
			if ( ReadyForBind )
				RebindAllHierarchy ( );
			
			_showing.Raise( this );
		}
		internal			void	DoFwdHide		( )	
		{
			try						{ OnFwdHide( ); }
			catch ( Exception ex )	{ Debug.LogException( ex ); }
		}
		internal			void	DoBackShow		( )	
		{
			try						{ OnBackShow( ); }
			catch ( Exception ex )	{ Debug.LogException( ex ); }
		}
		internal			void	DoHide			( )	
		{
			try						{ OnHide( ); }
			catch ( Exception ex )	{ Debug.LogException( ex ); }
			
			_hiding.Raise( this );
			_node = null;
		}
		
		internal			void	DoFirstChildShow( )	
		{
			try						{ OnHide( ); }
			catch ( Exception ex )	{ Debug.LogException( ex ); }
		}
		internal			void	DoLastChildHide	( )	
		{
			try						{ OnHide( ); }
			catch ( Exception ex )	{ Debug.LogException( ex ); }
		}
		
		protected virtual	void	OnShow			( )	{ }
		protected virtual	void	OnFwdHide		( )	{ }
		protected virtual	void	OnBackShow		( )	{ }
		protected virtual	void	OnHide			( )	{ }
		
		protected virtual	void	OnFirstChildShow( )	{ }
		protected virtual	void	OnLastChildHide	( )	{ }
		
		[Callable] public	void	Close			( )	
		{
			_node.Close();
		}
		public				void	CloseAndDestroy	( )	
		{
			Close();
			DestroyWhenStateWillHide(_node).Forget();
			return;

			static async UniTaskVoid DestroyWhenStateWillHide( FlowNode node )
			{
				await UniTask.WaitWhile( () => node.State.gameObject.activeSelf );
				Destroy( node.State.gameObject );
			}
		}

		public				void	RebindAllHierarchy	( )	
		{
			foreach ( var bb in gameObject.GetComponentsInChildren<APropertyBindableBehaviour>() )
			{
				if ( bb.gameObject == gameObject )	bb.MakeBindReadyAndRebindAll();
				else								bb.RebindAll();
			}
		}
		public override		String	ToString			( ) => _node.ToString();

		public record struct Opener( OpenCtx Ctx ) : IOpenerB
		{
			public		StateHandle		Open	( ) => Ctx.Open();
		}
	}
	
	public readonly record struct StateHandle( FlowNode Node )
	{
		internal	FlowNode	Node		{get;}			= Node; 
		
		public 		Boolean 	IsValid		=> Node.IsValid;
		public		State		State		=> Node.State;

		public			StateHandle	Close		( ) => !IsValid ? default : Node.Close();
		public override	String		ToString	( ) => $"StateHandle {Node.State}";
	}
	
	[AttributeUsage(AttributeTargets.Method)]
	public class StateTestAttribute: Attribute {}
}