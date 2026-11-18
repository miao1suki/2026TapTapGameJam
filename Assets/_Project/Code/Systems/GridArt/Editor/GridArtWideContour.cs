using System.Collections.Generic;
using UnityEngine;

namespace Project.GridArt.Editor
{
    /// <summary>Samples 32px artwork over two cells without stretching its native pixels.</summary>
    public static class GridArtWideContour
    {
        public static (int slot, Rect part) Resolve(Dictionary<Vector2Int,int> cells, Vector2Int p, int brush, int q)
        {
            bool Same(int x,int y) => GridArtRules.Same(cells,p+new Vector2Int(x,y),brush);
            int Air(int x,int y) { for(int d=1;d<=2;d++) if(!Same(x*d,y*d)) return d; return 0; }
            int left=Air(-1,0), right=Air(1,0), bottom=Air(0,-1), top=Air(0,1);
            // A single-cell strip keeps the compact opposing-edge art. Two wide
            // bands cannot fit inside its sixteen native pixels.
            if ((left==1 && right==1) || (top==1 && bottom==1)) return (-1,default);
            int qx=q%2, qy=q/2;
            float cx=qx*.5f+.25f, cy=qy*.5f+.25f;
            bool useLeft=left>0 && (right==0 || left-1+cx <= right-cx);
            bool useBottom=bottom>0 && (top==0 || bottom-1+cy <= top-cy);
            int horizontal=useLeft ? left : right, vertical=useBottom ? bottom : top;
            int sx=(p.x%2+2)%2*16+qx*8, sy=(p.y%2+2)%2*16+qy*8, slot=4;
            if(horizontal>0) sx=useLeft ? (left-1)*16+qx*8 : 32-right*16+qx*8;
            if(vertical>0) sy=useBottom ? (bottom-1)*16+qy*8 : 32-top*16+qy*8;
            if(horizontal>0 && vertical>0) slot=(useBottom?2:0)*3+(useLeft?0:2);
            else if(horizontal>0) slot=useLeft?3:5;
            else if(vertical>0) slot=useBottom?7:1;
            float cardinal=float.MaxValue;
            if(left>0) cardinal=Mathf.Min(cardinal,left-1+cx);
            if(right>0) cardinal=Mathf.Min(cardinal,right-cx);
            if(bottom>0) cardinal=Mathf.Min(cardinal,bottom-1+cy);
            if(top>0) cardinal=Mathf.Min(cardinal,top-cy);
            {
                float nearest=cardinal*cardinal;
                for(int y=-2;y<=2;y++) for(int x=-2;x<=2;x++)
                {
                    if(x==0 || y==0 || Same(x,y)) continue;
                    float dx=x<0 ? -x-1+cx : x-cx, dy=y<0 ? -y-1+cy : y-cy;
                    float distance=dx*dx+dy*dy;
                    if(distance>=nearest) continue;
                    nearest=distance;
                    slot=9+(x<0?0:1)+(y<0?0:2);
                    sx=x<0 ? (-x-1)*16+qx*8 : 32-x*16+qx*8;
                    sy=y<0 ? (-y-1)*16+qy*8 : 32-y*16+qy*8;
                }
            }
            return (slot,new Rect(sx/32f,sy/32f,.25f,.25f));
        }
    }
}
