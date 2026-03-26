using NUnit.Framework;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Animations;

public class Pown : Piece
{
    public override List<Vector2Int> GetAvailavleMoves(ref Piece[,] bord ,int tcx,int tcy)
    {
        List<Vector2Int> r = new List<Vector2Int>();
        int Direction = (Color == 0) ? 1 : -1;
        if (bord[posX,posY+Direction] == null)
            r.Add(new Vector2Int(posX, posY + Direction));
        if (bord[posX, posY + Direction] == null)
        {
            if (Color == 0 && posY == 1 && bord[posX, posY + (Direction * 2)] == null)
                r.Add(new Vector2Int(posX, posY + (Direction * 2)));
            if (Color == 1 && posY == 6 && bord[posX, posY + (Direction * 2)] == null)
                r.Add(new Vector2Int(posX, posY + (Direction * 2)));
        }
        if (posX != tcx-1)
        {
            if (bord[posX + 1, posY + Direction] != null && bord[posX + 1, posY + Direction].Color != Color)
                r.Add(new Vector2Int(posX + 1, posY + Direction));
        }
        if (posX != 0 )
        {
            if (bord[posX - 1, posY + Direction] != null && bord[posX - 1, posY + Direction].Color != Color)
                r.Add(new Vector2Int(posX - 1, posY + Direction));
        }
        return r;
    }

    public override SpecialMove GetSpecialMove(ref Piece[,] board, ref List<Vector2Int[]> moveList, ref List<Vector2Int> availableMoves)
    {
        int direction = (Color == 0) ? 1 : -1;
        if ((Color ==0 && posY == 6)|| (Color == 1 && posY ==1))
        {
            return SpecialMove.Promotion;
        }
        if (moveList.Count>0)
        {
            Vector2Int[] lastmove = moveList[moveList.Count - 1];
            if (board[lastmove[1].x,lastmove[1].y].type ==PieceType.Pawn)// ο τύπος του κομματιού της τελευταίας κίνησης είναι αγρότης
            {
                if (Mathf.Abs(lastmove[0].y - lastmove[1].y) == 2 )// αν η τελευταία κίνηση ήταν +2 γραμμές
                {
                    if (board[lastmove[1].x,lastmove[1].y].Color !=Color)// αν η τελευταία κίνηση έγινε από τον αντίπαλο
                    {
                        if (lastmove[1].y == posY)// αν και τα δύο πιόνια βρίσκονται στην ίδια σειρά
                        {
                            if (lastmove[1].x == posX-1)// αριστερά - en passant
                            {
                                availableMoves.Add(new Vector2Int(posX-1,posY+direction));
                                return SpecialMove.EnPassant;
                            }
                            if (lastmove[1].x == posX +1)// δεξιά - en passant
                            {
                                availableMoves.Add(new Vector2Int(posX + 1, posY + direction));
                                return SpecialMove.EnPassant;
                            }
                        }
                    }
                }
            }
        }
        return SpecialMove.None;
    }
}
