namespace Flexy.GameFlow;

public class TransitionHost
{
	internal	FlowNode		_node		= null!;
	internal	FlowNode		_tipNode	= null!;
	internal	FlowNode		_activeNode	= null!;
	
	private		Boolean			_doTransition;
	private		Boolean			_isInTransition;
	
	public		FlowNode		TipNode		=> _tipNode;
	public		FlowNode		ActiveNode	=> _activeNode;
	
	public		FlowNode		TryGoBack						( )		
	{
		if (_tipNode.Back != null && _tipNode.State.TryGoBack())
			_node.Graph.RemoveNodesUpTo(_tipNode, _tipNode.Back);

		return _tipNode;
	}
	public			void		TransitionNow					( )		
	{
		if (!_doTransition) 
			return;
		
		_doTransition = false;
		DoStateTransitions().Forget();
	}
	
	internal		void		ScheduleSwitchStates			( )		
	{
		_doTransition	= true;
	}
	internal async	UniTask		SwitchStatesAsyncInfiniteLoop	( )		
	{
		while (Application.isPlaying && _node.State)
		{
			// Change view at last update (before animations)
			// This will allow to make many changes in update and then only one view transition
			await UniTask.NextFrame( PlayerLoopTiming.LastUpdate );

			if (!_doTransition || _isInTransition)
				continue;

			while (_doTransition)
			{
				_doTransition	= false;
				await DoStateTransitions();
			}
		}
	}
	internal async	UniTask		DoStateTransitions				( )		
	{
		try						
		{
			_isInTransition = true;
			var tipNode = _tipNode;
				
			await SimpleTransition(_activeNode, _tipNode);
				
			_activeNode = tipNode;
		}
		catch ( Exception ex )	{ Debug.LogException( ex ); }
		finally { _isInTransition = false; }
	}
	
	internal static	UniTask		NodeStateHide			( FlowNode node, Boolean isForwardHide )	
	{
		var state			= node.State;

		//Debug.Log( $"[TransitionHost] {node} - {(isForwardHide? "Forward Hide": "Hide")}" );
		
		try
		{
			if (isForwardHide)	return state.DoForwardHide	();
			else				return state.DoHide			();
		}
		catch ( Exception ex )
		{
			Debug.LogException( ex );
			return default; 
		}
	}
	internal static	UniTask		NodeStateShow			( FlowNode node, Boolean isBackShow )		
	{
		var state	= node.State;
		state._node	= node;
		
		//Debug.Log( $"[TransitionHost] {node} - {(isBackShow? "Back Show": "Show")}" );
		
		try
		{
			if (isBackShow)	return state.DoBackShow	();
			else			return state.DoShow		();
		}
		catch (Exception ex) 
		{
			Debug.LogException(ex);
			return default; 
		}
		finally
		{
			node.WasShowed = true;
		}
	}

	private	async	UniTask		SimpleTransition		( FlowNode prevNode, FlowNode nextNode )	
	{
		if (prevNode == nextNode)
			return;
	
		var commonParent	= FlowNodeExt.FindNearestCommonParent(prevNode, nextNode);

		var closingBranchNode = prevNode;
		while (closingBranchNode != commonParent)
		{
			var isForwardHide	= closingBranchNode.IsOpened;
		
			try						{ await NodeStateHide(closingBranchNode, isForwardHide); } 
			catch (Exception ex)	{ Debug.LogException(ex); }

			var parent = closingBranchNode.Parent; 
			
			if (!isForwardHide && parent.FirstBaseChild == null && parent.ChildrenShowed)
				parent.State.DoLastChildHide(parent);
				
			closingBranchNode = parent;
		}
		
		var openingBranchNode = commonParent.FirstBaseChild.GetLastSiblingOrNull();
		
		while (openingBranchNode != null)
		{
			var isBackShow = _activeNode.IsInForwardOf(openingBranchNode);
		
			if (!isBackShow && !openingBranchNode.Parent.ChildrenShowed)
				openingBranchNode.Parent.State.DoFirstChildShow(openingBranchNode.Parent);
		
			try						{ await NodeStateShow( openingBranchNode, isBackShow ); } 
			catch (Exception ex)	{ Debug.LogException(ex); }
			
			openingBranchNode = openingBranchNode.FirstBaseChild.GetLastSiblingOrNull();
		}
		
		_activeNode = _tipNode;
	}
}