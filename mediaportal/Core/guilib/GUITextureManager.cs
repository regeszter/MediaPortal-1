//#define DO_RESAMPLE
using System;
using System.Net;
using System.Text;
using System.IO;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.Collections;
using Microsoft.DirectX;
using Microsoft.DirectX.Direct3D;
using Direct3D=Microsoft.DirectX.Direct3D;


namespace MediaPortal.GUI.Library
{
	/// <summary>
	/// 
	/// </summary>
  public class GUITextureManager 
  {
    class DownloadedImage
    {
      string    _FileName;
      string    _URL;
      DateTime  _DateDownloaded=DateTime.MinValue;
      int       _CacheTime = 60*30; //30minutes

      public DownloadedImage(string url)
      {
        URL=url;
        int pos=url.LastIndexOf("/");
        
        _FileName=GetTempFileName();
      }

      string GetTempFileName()
      {
        int x=0;
        while (true)
        {
          string tempFile=String.Format(@"thumbs\MPTemp{0}.gif",x);
          string tempFile2=String.Format(@"thumbs\MPTemp{0}.jpg",x);
          string tempFile3=String.Format(@"thumbs\MPTemp{0}.bmp",x);
          if (!System.IO.File.Exists(tempFile) && 
              !System.IO.File.Exists(tempFile2) &&
              !System.IO.File.Exists(tempFile3))
          {
            return tempFile;
          }
          ++x;
        }
      }
      
      
      public string FileName
      {
        get {return _FileName;}
        set {_FileName=value;}
      }
      
      public string URL
      {
        get { return _URL;}
        set {_URL=value;}
      }

      public int CacheTime
      {
        get { return _CacheTime;}
        set { _CacheTime=value;}
      }

      public bool ShouldDownLoad
      {
        get 
        {
          TimeSpan ts=DateTime.Now - _DateDownloaded;
          if (ts.TotalSeconds > CacheTime)
          {
            return true;
          }
          return false;
        }
      }

      public bool Download()
      {
        using (WebClient client = new WebClient())
        {
          try
          {
            try
            {
              System.IO.File.Delete(FileName);
            }
            catch(Exception)
            {
              Log.Write("DownloadedImage:Download() Delete failed:{0}", FileName);
            }

            client.DownloadFile(URL, FileName);
            try
            {
              string strExt="";
              string strContentType=client.ResponseHeaders["Content-type"].ToLower();
              if (strContentType.IndexOf("gif")>=0) strExt=".gif";
              if (strContentType.IndexOf("jpg")>=0) strExt=".jpg";
              if (strContentType.IndexOf("jpeg")>=0) strExt=".jpg";
              if (strContentType.IndexOf("bmp")>=0) strExt=".bmp";
              if (strExt.Length>0)
              {
                string strNewFile=System.IO.Path.ChangeExtension(FileName,strExt);
                if (!strNewFile.ToLower().Equals(FileName.ToLower()))
                {
                  try
                  {
                    System.IO.File.Delete(strNewFile);
                  }
                  catch(Exception)
                  {
                    Log.Write("DownloadedImage:Download() Delete failed:{0}", strNewFile);
                  }
                  System.IO.File.Move(FileName,strNewFile);
                  FileName=strNewFile;
                }
              }
            }
            catch(Exception)
            {
              Log.Write("DownloadedImage:Download() DownloadFile failed:{0}->{1}", URL,FileName);

            }
            _DateDownloaded=DateTime.Now;
            return true;
          } 
          catch(Exception ex)
          {
            Log.Write("download failed:{0}", ex.Message);
          }
        }
        return false;
      }
    }

    static ArrayList m_cache = new ArrayList();
    static ArrayList _DownloadCache = new ArrayList();
    static bool      _Disposed=false;
    const int        MAX_THUMB_WIDTH=512;
    const int        MAX_THUMB_HEIGHT=512;
    // singleton. Dont allow any instance of this class
    private GUITextureManager()
    {
    }

    ~GUITextureManager()
    {
      dispose(false);
    }

    static public void Dispose()
    {
      dispose(true);
      //GC.SuppressFinalize(this);
    }
    
    static void dispose(bool disposing)
    {
      if ( !_Disposed)
      {
        if (disposing)
        {
          Log.Write("  texturemanager:dispose()");
          foreach (CachedTexture cached in m_cache)
          {
            cached.Dispose();
          }
          m_cache.Clear();
        }
        _DownloadCache.Clear();

        string [] files=System.IO.Directory.GetFiles("thumbs","MPTemp*.*");
        if (files!=null)
        {
          foreach (string file in files)
          {
            try
            {
              System.IO.File.Delete(file);
            }
            catch(Exception) 
            {
            }
          }
        }
      }
      _Disposed=true;
    }

    static public void StartPreLoad()
    {
      //TODO
    }
    static public void EndPreLoad()
    {
      //TODO
    }

    static public Image Resample( Image imgSrc, int iMaxWidth, int iMaxHeight)
    {
      int iWidth=imgSrc.Width;
      int iHeight=imgSrc.Height;
      while (iWidth < iMaxWidth || iHeight < iMaxHeight)
      {
        iWidth *=2;
        iHeight*=2;
      }
      float fAspect= ((float)iWidth) / ((float)iHeight);
  		
      if (iWidth > iMaxWidth)
      {
        iWidth  = iMaxWidth;
        iHeight = (int)Math.Round( ( (float)iWidth) / fAspect);
      }

      if (iHeight > (int)iMaxHeight)
      {
        iHeight = iMaxHeight;
        iWidth  = (int)Math.Round(  fAspect * ( (float)iHeight) );
      }
        
      Bitmap result= new Bitmap(iWidth,iHeight);
      using (Graphics g = Graphics.FromImage(result))
      {
        g.CompositingQuality = System.Drawing.Drawing2D.CompositingQuality.HighQuality;
        g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
        g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.HighQuality;
        g.DrawImage(imgSrc, new Rectangle(0,0,iWidth,iHeight) );
      }
      return result;
    }

    static string GetFileName(string strfilename)
    {
      if (strfilename.Length==0) return "";
      if (strfilename == "-") return "";
      string strLow=strfilename.ToLower().Trim();
      if (strLow.IndexOf(@"http:")>=0 )
      {
        foreach (DownloadedImage image in _DownloadCache)
        {
          if (image.URL.Equals(strfilename))
          {
            if (image.ShouldDownLoad)
            {
              image.Download();
            }
            return image.FileName;
          }
        }
        DownloadedImage newimage = new DownloadedImage(strfilename);
        newimage.Download();
        _DownloadCache.Add(newimage);
        return newimage.FileName;
      }
      
      if (!System.IO.File.Exists(strfilename))
      {
        if (strfilename[1]!=':')
          return GUIGraphicsContext.Skin+@"\media\"+strfilename;
      }
      return strfilename;
    }

    static public int Load(string strFileNameOrg, long lColorKey, int iMaxWidth, int iMaxHeight)
    {
      string strFileName=GetFileName(strFileNameOrg);
      if (strFileName=="") return 0;

      for (int i=0; i < m_cache.Count;++i)
      {
        CachedTexture cached =(CachedTexture ) m_cache[i];
      
        if (cached.Name==strFileName) 
        {
          return cached.Frames;
        }
      }

      string strExtension =System.IO.Path.GetExtension(strFileName).ToLower();
      if (strExtension==".gif")
      {
        if (!System.IO.File.Exists(strFileName))
        {
          Log.Write("texture:{0} does not exists",strFileName);
          return 0;
        }

        Image theImage =null;
        try
        {
          theImage = Image.FromFile(strFileName);
          if (theImage!=null)
          {
            CachedTexture newCache = new CachedTexture();

            newCache.Name=strFileName;
            FrameDimension oDimension = new FrameDimension(theImage.FrameDimensionsList[0]);
            newCache.Frames=theImage.GetFrameCount(oDimension);
            int[] frameDelay = new int[newCache.Frames];
            for (int num2 = 0; (num2 < newCache.Frames); ++num2) frameDelay[num2]=0;

            int num1 = 20736;
            PropertyItem item1 = theImage.GetPropertyItem(num1);
            if (item1 != null)
            {
              byte[] buffer1 = item1.Value;
              for (int num2 = 0; (num2 < newCache.Frames); ++num2)
              {
                frameDelay[num2] = (((buffer1[(num2 * 4)] + (256 * buffer1[((num2 * 4) + 1)])) + (65536 * buffer1[((num2 * 4) + 2)])) + (16777216 * buffer1[((num2 * 4) + 3)]));
              }
            }
            for (int i=0; i < newCache.Frames; ++i)
            {
              theImage.SelectActiveFrame(oDimension,i);


              //load gif into texture
              using (System.IO.MemoryStream stream = new System.IO.MemoryStream())
              {
                theImage.Save(stream,System.Drawing.Imaging.ImageFormat.Png);
                ImageInformation info2 = new ImageInformation();
                stream.Flush();
                stream.Seek(0,System.IO.SeekOrigin.Begin);
                Direct3D.Texture texture=TextureLoader.FromStream(
                                                                  GUIGraphicsContext.DX9Device,
                                                                  stream,
                                                                  0,0,//width/height
                                                                  1,//mipslevels
                                                                  0,//Usage.Dynamic,
                                                                  Format.Dxt3,
                                                                  Pool.Managed,
                                                                  Filter.None,
                                                                  Filter.None,
                                                                  (int)lColorKey,
                                                                  ref info2);
                newCache.Width=info2.Width;
                newCache.Height=info2.Height;
                newCache[i]=new CachedTexture.Frame(texture, (frameDelay[i]/5)*50);
              }
            }
            
            theImage.Dispose();
            theImage=null;
            m_cache.Add(newCache);
            
            Log.Write("  texturemanager:added:"+strFileName + " total:"+m_cache.Count + " mem left:"+GUIGraphicsContext.DX9Device.AvailableTextureMemory.ToString() );
            return newCache.Frames;
          }
        }
        catch (Exception ex)
        {
          Log.Write("exception loading texture {0} err:{1} stack:{2}", strFileName, ex.Message,ex.StackTrace);
        }
        return 0;
      }

      if (System.IO.File.Exists(strFileName))
      {
        int iWidth, iHeight;
        Direct3D.Texture dxtexture=LoadGraphic(strFileName,lColorKey,iMaxWidth,iMaxHeight, out iWidth, out iHeight);
        if (dxtexture!=null)
        {
          CachedTexture newCache = new CachedTexture();
          newCache.Name=strFileName;
          newCache.Frames=1;
          newCache.Width=iWidth;
          newCache.Height=iHeight;
          newCache.texture=new CachedTexture.Frame(dxtexture,0);
          Log.Write("  texturemanager:added:"+strFileName + " total:"+m_cache.Count + " mem left:"+GUIGraphicsContext.DX9Device.AvailableTextureMemory.ToString() );
          m_cache.Add(newCache);
          return 1;
        }
      }
      return 0;
    }
    
    static Direct3D.Texture LoadGraphic(string strFileName,long lColorKey, int iMaxWidth,int iMaxHeight,out int iWidth, out int iHeight)
    {
      iWidth=0;
      iHeight=0;
      Image imgSrc=null;
      Direct3D.Texture texture=null;
      try
      {
#if DO_RESAMPLE
        imgSrc=Image.FromFile(strFileName);   
        if (imgSrc==null) return null;
				//Direct3D prefers textures which height/width are a power of 2
				//doing this will increases performance
				//So the following core resamples all textures to
				//make sure all are 2x2, 4x4, 8x8, 16x16, 32x32, 64x64, 128x128, 256x256, 512x512
				int w=-1,h=-1;
				if (imgSrc.Width >2   && imgSrc.Width < 4)  w=2;
				if (imgSrc.Width >4   && imgSrc.Width < 8)  w=4;
				if (imgSrc.Width >8   && imgSrc.Width < 16) w=8;
				if (imgSrc.Width >16  && imgSrc.Width < 32) w=16;
				if (imgSrc.Width >32  && imgSrc.Width < 64) w=32;
				if (imgSrc.Width >64  && imgSrc.Width <128) w=64;
				if (imgSrc.Width >128 && imgSrc.Width <256) w=128;
				if (imgSrc.Width >256 && imgSrc.Width <512) w=256;
				if (imgSrc.Width >512 && imgSrc.Width <1024) w=512;


				if (imgSrc.Height >2   && imgSrc.Height < 4)  h=2;
				if (imgSrc.Height >4   && imgSrc.Height < 8)  h=4;
				if (imgSrc.Height >8   && imgSrc.Height < 16) h=8;				
				if (imgSrc.Height >16  && imgSrc.Height < 32) h=16;
				if (imgSrc.Height >32  && imgSrc.Height < 64) h=32;
				if (imgSrc.Height >64  && imgSrc.Height <128) h=64;
				if (imgSrc.Height >128 && imgSrc.Height <256) h=128;
				if (imgSrc.Height >256 && imgSrc.Height <512) h=256;
				if (imgSrc.Height >512 && imgSrc.Height <1024) h=512;
				if (w>0 || h>0)
				{
					if (h > w) w=h;
					Log.Write("TextureManager: resample {0}x{1} -> {2}x{3} {4}",
												imgSrc.Width,imgSrc.Height, w,w,strFileName);

					Image imgResampled=Resample(imgSrc,w, h);
					imgSrc.Dispose();
					imgSrc=imgResampled;
					imgResampled=null;
				}
#endif
				
				Format fmt=Format.A8R8G8B8;
				if (IsTemporary(strFileName))
				{
					//fmt=Format.Dxt3;
					iMaxWidth=MAX_THUMB_WIDTH;
					iMaxHeight=MAX_THUMB_HEIGHT;
					imgSrc=Image.FromFile(strFileName);   
					if (imgSrc==null) return null;
					if (imgSrc.Width >= iMaxWidth || imgSrc.Height>=iMaxHeight)
					{
						Image imgResampled=Resample(imgSrc,iMaxWidth, iMaxHeight);
						imgSrc.Dispose();
						imgSrc=imgResampled;
						imgResampled=null;
					}
					//load jpg or png into texture
					using (System.IO.MemoryStream stream = new System.IO.MemoryStream())
					{
						imgSrc.Save(stream,System.Drawing.Imaging.ImageFormat.Png);
						ImageInformation info2 = new ImageInformation();
						stream.Flush();
						stream.Seek(0,System.IO.SeekOrigin.Begin);
						texture=TextureLoader.FromStream(GUIGraphicsContext.DX9Device,
							stream,
							0,0,//width/height
							1,//mipslevels
							0,//Usage.Dynamic,
							fmt,
							Pool.Managed,
							Filter.None,
							Filter.None,
							(int)lColorKey,
							ref info2);
						iWidth=info2.Width;
						iHeight=info2.Height;
					
						Log.Write("Texturemanager loaded temporay:{0} {1}x{2} format:{3}",
							strFileName,iWidth,iHeight,info2.Format);
					}
				}
				else
				{
					//fmt=GetCompression(strFileName);
					//fmt=Direct3D.Format.Dxt3;
					ImageInformation info2 = new ImageInformation();
					texture=TextureLoader.FromFile(GUIGraphicsContext.DX9Device,
																					strFileName,
																					0,0,//width/height
																					1,//mipslevels
																					0,//Usage.Dynamic,
																					fmt,
																					Pool.Managed,
																					Filter.None,
																					Filter.None,
																					(int)lColorKey,
																					ref info2);
					iWidth=info2.Width;
					iHeight=info2.Height;
					/*
					if (iWidth > (GUIGraphicsContext.Width/2) ||
						iHeight> (GUIGraphicsContext.Height/2) )
					{
						texture.Dispose();
						fmt=Direct3D.Format.A8R8G8B8;
						texture=TextureLoader.FromFile(GUIGraphicsContext.DX9Device,
							strFileName,
							0,0,//width/height
							1,//mipslevels
							0,//Usage.Dynamic,
							fmt,
							Pool.Managed,
							Filter.None,
							Filter.None,
							(int)lColorKey,
							ref info2);
						iWidth=info2.Width;
						iHeight=info2.Height;
					}
					Log.Write("Texturemanager loaded:{0} {1}x{2} format:{3}",
												strFileName,iWidth,iHeight,info2.Format);*/
					
				}
      }
      catch(Exception ex)
      {
        Log.Write("TextureManage:LoadGraphic({0}) failed:{1}", strFileName,ex.ToString());
      }
      finally
      {
        if (imgSrc!=null)
        {
          imgSrc.Dispose();
        }
      }
			return texture;
		}

    static public Image GetImage(string strFileNameOrg)
    {
      string strFileName=GetFileName(strFileNameOrg);
      if (strFileName=="") return null;
      
      for (int i=0; i < m_cache.Count;++i)
      {
        CachedTexture cached=(CachedTexture)m_cache[i];
        if (cached.Name==strFileName) 
        {
          if (cached.image!=null)
            return cached.image;
          else
          {
            
						try
						{
							cached.image = Image.FromFile(strFileName);
						}
						catch(Exception ex)
            {
              Log.Write("TextureManage:GetImage({0}) failed:{1}", strFileName,ex.ToString());
              return null;
            }
            return cached.image;
          }
        }
      }

      if (!System.IO.File.Exists(strFileName)) return null;
      Image img=null;
			try{
				img= Image.FromFile(strFileName);
			}
			catch(Exception ex)
      {
        Log.Write("TextureManage:GetImage({0}) failed:{1}", strFileName,ex.ToString());
        return null;
      }
      if (img!=null)
      {
        CachedTexture newCache = new CachedTexture();
        newCache.Frames=1;
        newCache.Name=strFileName;
        newCache.Width=img.Width;
        newCache.Height=img.Height;
				newCache.image=img;
				Log.Write("  texturemanager:added:"+strFileName + " total:"+m_cache.Count + " mem left:"+GUIGraphicsContext.DX9Device.AvailableTextureMemory.ToString() );
        m_cache.Add(newCache);
        return img;
      }
      return null;
    }

    static public CachedTexture.Frame GetTexture(string strFileNameOrg,int iImage, out int iTextureWidth,out int iTextureHeight)
    {
      iTextureWidth=0;
      iTextureHeight=0;

      string strFileName=GetFileName(strFileNameOrg);
      if (strFileName=="") return null;
      
      for (int i=0; i < m_cache.Count;++i)
      {
        CachedTexture cached=(CachedTexture)m_cache[i];
        if (cached.Name==strFileName) 
        {
          iTextureWidth=cached.Width;
          iTextureHeight=cached.Height;
					return (CachedTexture.Frame)cached[iImage];
        }
      }
      return null;
		}

		static public void ReleaseTexture(string strFileName)
    {
      if (strFileName==String.Empty) return;
      try
      {
				bool continueRemoving=false;
				do
				{
					continueRemoving=false;
					foreach (CachedTexture cached in m_cache)
					{
						if ( cached.Name.Equals(strFileName) )
						{
							Log.Write("texturemanager:dispose:{0} frames:{1} total:{2} mem left:{3}",cached.Name,cached.Frames,m_cache.Count ,GUIGraphicsContext.DX9Device.AvailableTextureMemory.ToString() );
							m_cache.Remove(cached);
							cached.Dispose();
							continueRemoving=true;
							break;
						}
					}
				} while (continueRemoving);
      }
      catch(Exception ex)
      {
          Log.Write("TextureManage:ReleaseTexture({0}) failed:{1}", strFileName,ex.ToString());
      }
		}
		
    static public void PreLoad(string strFileName)
    {
      //TODO
		}

		static public void CleanupThumbs()
		{
      Log.Write("texturemanager:CleanupThumbs()");
      try
      {
        ArrayList newCache=new ArrayList();
        foreach (CachedTexture cached in m_cache)
        {
          if ( IsTemporary(cached.Name) )
          { 
            Log.Write("texturemanager:dispose:"+ cached.Name+ " total:"+m_cache.Count + " mem left:"+GUIGraphicsContext.DX9Device.AvailableTextureMemory.ToString() );
            cached.Dispose();
          }
          else
          {
            newCache.Add(cached);
          }
        }
        m_cache.Clear();
        m_cache=newCache;
      }
      catch(Exception ex)
      {
        Log.Write("TextureManage:CleanupThumbs() failed:{0}", ex.ToString());
      }
		}

    static public bool IsTemporary(string strFileName)
    {
      if ( strFileName.ToLower().IndexOf("folder.jpg")>0 || 
           strFileName.ToLower().IndexOf(".tbn")>0 || 
           strFileName.ToLower().IndexOf(@"xmltv\") >=0 ||
           strFileName.ToLower().IndexOf(@"thumbs\") >=0)
      {
        return true;
      }
      return false;
    }
		static Direct3D.Format GetCompression(string filename)
		{
			try
			{
				int pos = filename.LastIndexOf(@"\");
				if (pos >=0) filename=filename.Substring(pos+1);
				string line;
				string textureInfo=String.Format(@"{0}\media\textures.info", GUIGraphicsContext.Skin);
				Encoding fileEncoding = Encoding.Default;
				FileStream stream = File.Open(textureInfo,FileMode.Open,FileAccess.Read,FileShare.Read);
				if (stream==null) return Direct3D.Format.A8R8G8B8;
				StreamReader file = new StreamReader(stream, fileEncoding, true);
				do
				{
					line=file.ReadLine();
					if (line==null) break;
					string[] parts=line.Split( new char[]{'='});
					if (parts!=null && parts.Length==2)
					{
						if (parts[0] == filename)
						{
							file.Close();
							int fmt=Int32.Parse(parts[1]);
							return (Direct3D.Format)fmt;
						}
					}
				} while (line!=null);

				file.Close();
			}
			catch(Exception)
			{}

			return Direct3D.Format.A8R8G8B8;
		}
	}
}
