using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System;
using System.IO;

public static class Utility {

    public static int Clampi(int v, int min, int max)
    {
        return v < min ? min : (v > max ? max : v);
    }

    public static T Log<T>(T param, string message = "") {
		Debug.Log(message +  param.ToString());
		return param;
	}

    public static Quaternion LookATRot2D(Transform dest,Transform origin)
    {
        var dir = dest.transform.position - origin.transform.position;
        dir.Normalize();

        float rotZ = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;

        return Quaternion.Euler(0, 0, rotZ +90);
    }

    public static float ConsumeFuelEnergy(float tank,float ratio)
    {
        
        var current = tank < 0 ? 0 : tank - ratio * Time.fixedDeltaTime;
        return current;
    }

    public static int GetRandomIndexByChance(float[] chance)
    {
        var random = UnityEngine.Random.value;
        float value = 0;

        for (int i = 0; i < chance.Length; i++)
        {
            value += chance[i];
            if (value > random)
            {
                return i;
            }
        }

        return -1;
    }

    public static float[] GetNormalizedChanceArray(float[] chanceArray)
    {
        float acum = 0;
        for (int i = 0; i < chanceArray.Length; i++)
        {
            acum += chanceArray[i];
        }

        if (acum != 1)
        {
            for (int i = 0; i < chanceArray.Length; i++)
            {
                chanceArray[i] = chanceArray[i] / acum;
            }
        }

        return chanceArray;
    }

    public static  Vector2 WorldToCanvasPosition(this RectTransform canvas, Camera camera, Vector3 position) {
        //Code From : http://answers.unity3d.com/questions/799616/unity-46-beta-19-how-to-convert-from-world-space-t.html
        
        //Vector position (percentage from 0 to 1) considering camera size.
        //For example (0,0) is lower left, middle is (0.5,0.5)
        Vector2 temp = camera.WorldToViewportPoint(position);
 
        //Calculate position considering our percentage, using our canvas size
        //So if canvas size is (1100,500), and percentage is (0.5,0.5), current value will be (550,250)
        temp.x *= canvas.sizeDelta.x;
        temp.y *= canvas.sizeDelta.y;
 
        //The result is ready, but, this result is correct if canvas recttransform pivot is 0,0 - left lower corner.
        //But in reality its middle (0.5,0.5) by default, so we remove the amount considering cavnas rectransform pivot.
        //We could multiply with constant 0.5, but we will actually read the value, so if custom rect transform is passed(with custom pivot) , 
        //returned value will still be correct.
 
        temp.x -= canvas.sizeDelta.x * canvas.pivot.x;
        temp.y -= canvas.sizeDelta.y * canvas.pivot.y;
 
        return temp;
    }

	public static IEnumerable<Src> Generate<Src>(Src seed, Func<Src, Src> generator) {
		while (true) {
			yield return seed;
			seed = generator(seed);
		}
	}

    public static IEnumerable<Tuple<int, int,int, T>> LazyMatrix<T>(T[,,] matrix)
    {
        for (int i = 0; i < matrix.GetLength(0); i++)
            for (int j = 0; j < matrix.GetLength(1); j++)
                for (int b = 0; b < matrix.GetLength(2); b++)
                    yield return Tuple.Create(i, j,b, matrix[i, j,b]);

    }
    public static IEnumerable<Tuple<int, int, T>> LazyMatrix<T>(T[,] matrix)
    {
        for (int i = 0; i < matrix.GetLength(0); i++)
        for (int j = 0; j < matrix.GetLength(1); j++)
      
            yield return Tuple.Create(i, j, matrix[i, j]);

    }

    public static string[] GetAllAssetsNamesInPath(string path)
    {

        List<String> names = new List<string>();
        string[] fileEntries = Directory.GetFiles(Application.dataPath + "/" + path);
        
        foreach (string fileName in fileEntries)
        {
            int index = fileName.LastIndexOf("\\");
            string assetFileName = "";
            if (index > 0)
            {
                assetFileName = fileName.Substring(index + 1);
                var pIndex = assetFileName.LastIndexOf(".");
                char splitter = '.';
                var nameParts = assetFileName.Split(splitter);
               assetFileName = nameParts.Length <= 2 ?  nameParts[0] :  "";
            }

            if (assetFileName != "")
                names.Add(assetFileName);   
        }
      
        return names.ToArray();
    }



}
