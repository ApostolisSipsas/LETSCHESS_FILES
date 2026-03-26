using System.Collections.Generic;
using UnityEngine;

public class Bishop : Piece
{
    public override List<Vector2Int> GetAvailavleMoves(ref Piece[,] bord, int tcx, int tcy)
    {
        List<Vector2Int> r = new List<Vector2Int>();
        // Πάνω δεξιά
        for (int x = posX +1 ,y = posY+1; x<tcx && y<tcy;x++,y++)
        {
            if (bord[x, y] == null)
                r.Add(new Vector2Int(x,y));
            else
            {
                if (bord[x, y].Color != Color)
                    r.Add(new Vector2Int(x, y));
                break;
            }
        }
        // Πάνω αριστερά
        for (int x = posX - 1, y = posY + 1; x >=0 && y < tcy; x--, y++)
        {
            if (bord[x, y] == null)
                r.Add(new Vector2Int(x, y));
            else
            {
                if (bord[x, y].Color != Color)
                    r.Add(new Vector2Int(x, y));
                break;
            }
        }
        // Κάτω δεξιά
        for (int x = posX + 1, y = posY - 1; x <tcx && y>=0; x++, y--)
        {
            if (bord[x, y] == null)
                r.Add(new Vector2Int(x, y));
            else
            {
                if (bord[x, y].Color != Color)
                    r.Add(new Vector2Int(x, y));
                break;
            }
        }

        // Κάτω αριστερά
        for (int x = posX - 1, y = posY - 1; x >=0 && y >= 0; x--, y--)
        {
            if (bord[x, y] == null)
                r.Add(new Vector2Int(x, y));
            else
            {
                if (bord[x, y].Color != Color)
                    r.Add(new Vector2Int(x, y));
                break;
            }
        }
        return r;
    }
}
