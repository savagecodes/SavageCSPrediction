using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public static class ExtensionMethods {

    public static Vector3 ClampVector3Values(this Vector3 v, float min, float max) {

        v.x = Mathf.Clamp(v.x, min, max);
        v.y = Mathf.Clamp(v.y, min, max);
        v.z = Mathf.Clamp(v.z, min, max);
        return v;
    }
    
    public static Vector2 ClampVector2Values(this Vector2 v, float min, float max) {

        v.x = Mathf.Clamp(v.x, min, max);
        v.y = Mathf.Clamp(v.y, min, max);
        return v;
    }
    

    public static int Layer (this Collider c){
         return c.gameObject.layer;
    }

    public static int Layer (this Collision c){
         return c.gameObject.layer;
    }

    public static Ray SetDirection(this Ray r, Vector3 dir ) {

        r.direction = dir;
        return r;
    }

    public static Ray SetOrigin(this Ray r, Vector3 o)
    {
        r.origin = o;
        return r;
    }

    public static IEnumerable<Transform> RecursiveWalker(this Transform parent)
    {
        foreach (Transform child in parent)
        {
            foreach (Transform grandchild in RecursiveWalker(child))
                yield return grandchild;
            yield return child;
        }
    }

}
