namespace Flexy.GameFlow;

internal static class LiteTransitions
{
	internal static	void		DoStateTransitions		( ref FlowNode mainLineActive, FlowNode mainLineTip )	
	{
		var prevNode		= mainLineActive; 
		var nextNode		= mainLineTip;
	
		if (prevNode == nextNode)
			return;
	
		var isMoveForward	= IsForward(prevNode, nextNode);
		var commonParent	= FindNearestCommonParent( prevNode, nextNode );

		var closingBranchNode = prevNode;
		while (closingBranchNode != commonParent)
		{
			try{ closingBranchNode.State.gameObject.SetActive(false);	} catch (Exception ex) { Debug.LogException(ex); }
			try{ NodeStateHide(closingBranchNode, isMoveForward);		} catch (Exception ex) { Debug.LogException(ex); }

			closingBranchNode = closingBranchNode.Parent;
			
			if (!isMoveForward && closingBranchNode.FirstChild == null)
				closingBranchNode.State.DoLastChildHide();
		}
		
		var openingBranchNode = commonParent.FirstChild!.GetLastSibling();
		
		if (openingBranchNode == null)
			mainLineActive = commonParent;
		
		while (openingBranchNode != null)
		{
			mainLineActive = openingBranchNode;
		
			if (isMoveForward && openingBranchNode.Parent.FirstChild!.NextSibling == null)
				openingBranchNode.Parent.State.DoFirstChildShow();
		
			try{ NodeStateShow( openingBranchNode, isMoveForward );		} catch (Exception ex) { Debug.LogException(ex); }
			try{ openingBranchNode.State.gameObject.SetActive( true );	} catch (Exception ex) { Debug.LogException(ex); }
			
			openingBranchNode = openingBranchNode.FirstChild.GetLastSiblingOrNull();
		}
	}
	
	private static	Boolean		IsForward				( FlowNode prev, FlowNode next )						
	{
		for (var iter = prev.Forward; iter != null; iter = iter.Forward)
		{
			if (iter == next)
				return true;
		}
		
		return false;
	}
	private static	FlowNode	FindNearestCommonParent	( FlowNode a, FlowNode b )								
	{
		var aSet = new HashSet<FlowNode>();

		for ( ; a != null; a = a.Parent)
			aSet.Add( a );

		for ( ; b != null; b = b.Parent)
			if (aSet.Contains( b ))
				return b;

		throw new InvalidOperationException("Graph broken, can not find common parent, it must be at least one common parent -> Root of the graph ");
	}
	private static	void		NodeStateHide			( FlowNode prevNode, Boolean isMoveForward )			
	{
		var state = prevNode.State;
		
		try
		{
			if( isMoveForward )	state.DoFwdHide( );
			else				state.DoHide( );
		}
		catch ( Exception ex ) { Debug.LogException( ex ); }
	}
	private static	void		NodeStateShow			( FlowNode nextNode, Boolean isMoveForward )			
	{
		var nextWasShown		= nextNode.WasShown;
		nextNode.State._node	= nextNode;
		nextNode.WasShown		= true;
			
		var state = nextNode.State;
		
		if (!nextWasShown && !isMoveForward)
			try						{ state.DoShow(); }
			catch (Exception ex)	{ Debug.LogException(ex); }

		try
		{
			if (isMoveForward)	state.DoShow();
			else				state.DoBackShow();
		}
		catch (Exception ex) { Debug.LogException(ex); }
	}
}