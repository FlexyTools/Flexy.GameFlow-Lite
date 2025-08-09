namespace Flexy.GameFlow;

public record struct LibCtx ( State Src )
{
	public Service_GameFlow	Service			=> Src.Node.Graph.Service;

	public State.Opener		GetByUid		( String id )					=> Service.GetOpener_FromId( id, Src );
	public State.Opener		GetState<T>		( )	where T: State				=> Service.GetOpener_FromStateType<T>( Src );
	public T				GetOpener<T>	( )	where T: struct, IOpener	=> Service.GetOpener_FromOpenerType<T>( Src );
	
	public static LibCtx	FromWidget		( MonoBehaviour w )				
	{
		var panel = w.GetComponentInParent<State>();
		return new( panel );
	}
	
	public static implicit operator  LibCtx( State panel ) => new ( panel );
}