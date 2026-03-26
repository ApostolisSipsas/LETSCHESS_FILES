using System.Collections.Generic;
using UnityEngine;

public class Knight :  Piece
{
    public override List<Vector2Int> GetAvailavleMoves(ref Piece[,] bord, int tcx, int tcy)
    {
        List<Vector2Int> r = new List<Vector2Int>();

        // Πάνω δεξιά (1 δεξιά, 2 πάνω)
        int x = posX + 1;
        int y = posY + 2;
        if(x< tcx && y < tcy)
            if (bord[x, y] == null || bord[x, y].Color != Color)
                r.Add(new Vector2Int(x, y));


   
        x = posX + 2;
        y = posY + 1;
        if (x < tcx && y < tcy)
            if (bord[x, y] == null || bord[x, y].Color != Color)
                r.Add(new Vector2Int(x, y));
        // Πάνω αριστερά

        x = posX -1;
        y = posY + 2;
        if (x >=0 && y < tcy)
            if (bord[x, y] == null || bord[x, y].Color != Color)
                r.Add(new Vector2Int(x, y));

        x = posX - 2;
        y = posY + 1;
        if (x >= 0 && y < tcy)
            if (bord[x, y] == null || bord[x, y].Color != Color)
                r.Add(new Vector2Int(x, y));

        // Κάτω δεξιά

        x = posX + 1;
        y = posY - 2;
        if (x < tcx && y >= 0)
            if (bord[x, y] == null || bord[x, y].Color != Color)
                r.Add(new Vector2Int(x, y));


        x = posX  + 2;
        y = posY -1;
        if (x < tcx && y >=0 )
            if (bord[x, y] == null || bord[x, y].Color != Color)
                r.Add(new Vector2Int(x, y));

        // Κάτω αριστερά

        x = posX - 1;
        y = posY - 2;
        if (x >=0 && y >= 0)
            if (bord[x, y] == null || bord[x, y].Color != Color)
                r.Add(new Vector2Int(x, y));

        x = posX - 2;
        y = posY - 1;
        if (x >= 0 && y >= 0)
            if (bord[x, y] == null || bord[x, y].Color != Color)
                r.Add(new Vector2Int(x, y));

        return r;
    }
}
