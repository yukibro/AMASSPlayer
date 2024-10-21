using System;
using System.IO;
using UnityEngine;
using NumSharp;
using System.IO.Compression;

public static class NPArrayHelper
{

    public static T[,] ConvertArr<T>(object[,] arr) where T : unmanaged
    {
        var result = new T[arr.GetLength(0), arr.GetLength(1)];
        for (int i = 0; i < arr.GetLength(0); i++)
        {
            for (int j = 0; j < arr.GetLength(1); j++)
            {
                result[i, j] = (T)arr[i, j];
            }
        }
        return result;
    }
    public static Quaternion[,] AA_Arr_to_QuaternionArr(float[,] arr, bool isUnityCoordinateSystem = false)
    {
        Debug.Assert(arr.GetLength(1) % 3 == 0);
        var result = new Quaternion[arr.GetLength(0), arr.GetLength(1) / 3];
        for (int i = 0; i < result.GetLength(0); i++)
        {
            for (int j = 0; j < result.GetLength(1); j++)
            {
                if(isUnityCoordinateSystem)
                {
                    var v = new Vector3(arr[i, j * 3 + 0], arr[i, j * 3 + 1], arr[i, j * 3 + 2]);
                    result[i, j] = Quaternion.AngleAxis(v.magnitude / Mathf.PI * 180, v.normalized);
                }
                else
                {
                    var v = new Vector3(arr[i, j * 3 + 0], -arr[i, j * 3 + 1], -arr[i, j * 3 + 2]);
                    result[i, j] = Quaternion.AngleAxis(v.magnitude / Mathf.PI * 180, v.normalized);
                }
            }
        }
        return result;
    }
    public static Quaternion[,] Rotmat_Arr_to_QuaternionArr(float[,] arr)
    {
        Debug.Assert(arr.GetLength(1) % 9 == 0);
        if (arr.GetLength(1) % 9 != 0) throw new ArgumentException($"length %9 != 0, length was {arr.GetLength(1)}");
        var result = new Quaternion[arr.GetLength(0), arr.GetLength(1) / 9];
        for (int i = 0; i < result.GetLength(0); i++)
        {
            for (int j = 0; j < result.GetLength(1); j++)
            {
                var m = new Matrix4x4();
                m.SetRow(0, new Vector4(arr[i, j * 9 + 0], arr[i, j * 9 + 1], arr[i, j * 9 + 2], 0));
                m.SetRow(1, new Vector4(arr[i, j * 9 + 3], arr[i, j * 9 + 4], arr[i, j * 9 + 5], 0));
                m.SetRow(2, new Vector4(arr[i, j * 9 + 6], arr[i, j * 9 + 7], arr[i, j * 9 + 8], 0));
                m.SetRow(3, new Vector4(0, 0, 0, 1));
                result[i, j] = m.rotation;
            }
        }
        return result;
    }
    public static Quaternion[,] StrippedRotmat_Arr_to_QuaternionArr(float[,] arr)
    {
        Debug.Assert(arr.GetLength(1) % 6 == 0);
        if (arr.GetLength(1) % 6 != 0) throw new ArgumentException($"length %9 != 0 {arr.GetLength(1)}");
        var result = new Quaternion[arr.GetLength(0), arr.GetLength(1) / 6];
        for (int i = 0; i < result.GetLength(0); i++)
        {
            for (int j = 0; j < result.GetLength(1); j++)
            {
                var m = new Matrix4x4();
                Vector3 a = new Vector3(arr[i, j * 6 + 0], arr[i, j * 6 + 1], arr[i, j * 6 + 2]);
                Vector3 b = new Vector3(arr[i, j * 6 + 3], arr[i, j * 6 + 4], arr[i, j * 6 + 5]);
                Vector3 c = Vector3.Cross(a, b);
                m.SetRow(0, new Vector4(a.x, a.y, a.z, 0));
                m.SetRow(1, new Vector4(b.x, b.y, b.z, 0));
                m.SetRow(2, new Vector4(c.x, c.y, c.z, 0));
                m.SetRow(3, new Vector4(0, 0, 0, 1));
                result[i, j] = m.rotation;
            }
        }
        return result;
    }
    public static T[,] Cast2dArr<T>(Array arr)
    {
        var result = new T[arr.GetLength(0), arr.GetLength(1)];
        for (int i = 0; i < arr.GetLength(0); i++)
        {
            for (int j = 0; j < arr.GetLength(1); j++)
            {
                result[i, j] = (T)arr.GetValue(i, j);
            }
        }
        return result;
    }
    
    public static T[] Cast1dArr<T>(Array arr)
    {
        var result = new T[arr.GetLength(0)];
        for (int i = 0; i < arr.GetLength(0); i++)
        {
            result[i] = (T)arr.GetValue(i);
        }
        return result;
    }
    public static T[,] ZippedNPY2DArrType<T>(ZipArchive archive, string filename) where T : unmanaged
    {
        using (Stream transStream = archive.GetEntry(filename).Open())
        {
            NDArray npyContent = np.load(transStream);
            if(npyContent == null)
                throw new ArgumentException("File not found in zip archive: " + filename);
            var arr = npyContent.ToMuliDimArray<T>();
            return Cast2dArr<T>(arr);
        }
        throw new ArgumentException("File is not 2Darray: " + filename);
    }
    public static T[] ZippedNPY1DArrType<T>(ZipArchive archive, string filename) where T : unmanaged
    {
        using (Stream transStream = archive.GetEntry(filename).Open())
        {
            NDArray npyContent = np.load(transStream);
            if(npyContent == null)
                throw new ArgumentException("File not found in zip archive: " + filename);
            var arr = npyContent.ToArray<T>();
            return Cast1dArr<T>(arr);
        }
        throw new ArgumentException("File is not 1Darray: " + filename);
    }
}
