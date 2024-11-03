// Advanced Resource Checker
using System;
using System.Linq;
using System.IO;
using UnityEngine;
using UnityEngine.UI;
#if UNITY_EDITOR
using UnityEditor;
#if RESOURCECHECKER_EXPORT_FBX
using UnityEditor.Formats.Fbx.Exporter;
#endif
#endif
#if RESOURCECHECKER_TIMELINE
using UnityEngine.Playables;
using UnityEngine.Timeline;
#endif
using System.Collections.Generic;
using System.Reflection;
using Object = UnityEngine.Object;

public class TextureDetails : IEquatable<TextureDetails>
{
	public bool isCubeMap;
	public bool hasAlphaWrongSetting;
	public int memSizeKB;
	public Texture texture;
	public TextureFormat format;
	public int mipMapCount;
	public List<Object> FoundInMaterials=new List<Object>();
	public List<Object> FoundInRenderers=new List<Object>();
	public List<Object> FoundInAnimators = new List<Object>();
	public List<Object> FoundInScripts = new List<Object>();
	public List<Object> FoundInGraphics = new List<Object>();
	public List<Object> FoundInButtons = new List<Object>();
	public bool isSky;
	public bool instance;
	public bool isgui;
	public bool hasAlpha;

	public TextureDetails()
	{

	}

	public bool Equals(TextureDetails other)
	{
		return texture != null && other.texture != null &&
			texture.GetNativeTexturePtr() == other.texture.GetNativeTexturePtr();
	}

	public override int GetHashCode()
	{
		return (int)texture.GetNativeTexturePtr();
	}

	public override bool Equals(object obj)
	{
		return Equals(obj as TextureDetails);
	}
};

public class MaterialDetails
{

	public Material material;
	public String shaderName;
	public String shaderBrand;
	public List<Renderer> FoundInRenderers=new List<Renderer>();
	public List<Graphic> FoundInGraphics=new List<Graphic>();
	public bool instance;
	public bool isgui;
	public bool isSky;

	string[] _validKeywords;
	string[] _invalidKeywords;
	public string[] validKeywords => _validKeywords ??= CollectKeywords(false);
	public string[] invalidKeywords => _invalidKeywords ??= CollectKeywords(true);

	public MaterialDetails()
	{
		instance = false;
		isgui = false;
		isSky = false;
	}

	string[] CollectKeywords(bool value)
	{
		return (this.material != null) ? (this.material.shaderKeywords.Where(x => this.material.IsKeywordEnabled(x) == value).ToArray()) : null;
	}
};

public class MeshDetails
{

	public Mesh mesh;

	public List<MeshFilter> FoundInMeshFilters=new List<MeshFilter>();
	public List<SkinnedMeshRenderer> FoundInSkinnedMeshRenderer=new List<SkinnedMeshRenderer>();
	public bool instance;

	public MeshDetails()
	{
		instance = false;
	}
};

public class AnimationClipDetails
{

	public AnimationClip animationClip;

	public List<MonoBehaviour> FoundInMonoBehaviour = new List<MonoBehaviour>();
#if RESOURCECHECKER_TIMELINE
	public List<PlayableAsset> FoundInPlayableAsset = new List<PlayableAsset>();
#endif
	public bool instance;

	public AnimationClipDetails()
	{
		instance = false;
	}
};

public class AudioClipDetails
{

	public AudioClip audioClip;

	public List<MonoBehaviour> FoundInMonoBehaviour = new List<MonoBehaviour>();
#if RESOURCECHECKER_TIMELINE
	public List<PlayableAsset> FoundInPlayableAsset = new List<PlayableAsset>();
#endif
	public bool instance;

	public AudioClipDetails()
	{
		instance = false;
	}
};

public class MissingGraphic{
	public Transform Object;
	public string type;
	public string name;
}

public class AdvancedResourceChecker : EditorWindow {


	string[] inspectToolbarStrings = {"Textures", "Materials","Shader","Meshes","AnimationClips","AudioClips"};
	string[] inspectToolbarStrings2 = {"Textures", "Materials","Shader","Meshes","AnimationClips","AudioClips", "Missing"};

	enum InspectType 
	{
		Textures,Materials,Shaders,Meshes,AnimationClips,AudioClips,Missing
	};

	bool _includeDisabledObjects=true;
	bool _includeSpriteAnimations=true;
	bool _includeScriptReferences=true;
	bool _includeGuiElements=true;
	bool thingsMissing = false;

	InspectType _activeInspectType=InspectType.Textures;

	float ThumbnailWidth=40;
	float ThumbnailHeight=40;

	List<TextureDetails> ActiveTextures=new List<TextureDetails>();
	List<MaterialDetails> ActiveMaterials=new List<MaterialDetails>();
	List<MeshDetails> ActiveMeshDetails=new List<MeshDetails>();
	List<AnimationClipDetails> ActiveAnimationClips = new List<AnimationClipDetails>();
	List<AudioClipDetails> ActiveAudioClips = new List<AudioClipDetails>();
	List<MissingGraphic> MissingObjects = new List<MissingGraphic> ();
	private HashSet<string> printedMaterials;

	Vector2 textureListScrollPos=new Vector2(0,0);
	Vector2 materialListScrollPos=new Vector2(0,0);
	Vector2 shaderListScrollPos = new Vector2(0, 0);
	Vector2 meshListScrollPos=new Vector2(0,0);
	Vector2 animationClipListScrollPos = new Vector2(0, 0);
	Vector2 audioClipListScrollPos = new Vector2(0, 0);
	Vector2 missingListScrollPos = new Vector2 (0,0);

	int TotalTextureMemory=0;
	int TotalMeshVertices=0;
	int shaderCount = 0;
	bool shaderChecked;
	int TotalAnimationClipMemory = 0;

	bool ctrlPressed=false;

	static int MinWidth=475;
	Color defColor;

	bool collectedInPlayingMode;

	AssetSizeCache _assetSizeCache = new();

	const int PageDiv = 50;
	int _currentTexturePage;
	int _currentMaterialPage;
	int _currentMeshPage;
	int _currentAnimationClipPage;
	int _currentAudioClipPage;

	string _filterText = string.Empty;
	bool _ignoreCase = true;

	static string strActiveInspectType = "strActiveInspectType";
	static string strIncludeDisabledObjects = "IncludeDisabledObjects";
	static string strIncludeSpriteAnimations = "IncludeSpriteAnimations";
	static string strIncludeScriptReferences = "IncludeScriptReferences";
	static string strIncludeGuiElements = "IncludeGuiElements";

	InspectType ActiveInspectType
	{
		get => _activeInspectType;
		set => PlayerPrefs_SetInspectType(strActiveInspectType, ref _activeInspectType, value);
	}

	bool IncludeDisabledObjects
	{
		get => _includeDisabledObjects;
		set => PlayerPrefs_SetBool(strIncludeDisabledObjects, ref _includeDisabledObjects, value);
	}
	bool IncludeSpriteAnimations
	{
		get => _includeSpriteAnimations;
		set => PlayerPrefs_SetBool(strIncludeSpriteAnimations, ref _includeSpriteAnimations, value);
	}
	bool IncludeScriptReferences
	{
		get => _includeScriptReferences;
		set => PlayerPrefs_SetBool(strIncludeScriptReferences, ref _includeScriptReferences, value);
	}
	bool IncludeGuiElements
	{
		get => _includeGuiElements;
		set => PlayerPrefs_SetBool(strIncludeGuiElements, ref _includeGuiElements, value);
	}

	[MenuItem ("Tools/Advanced Resource Checker")]
	static void Init ()
	{  
		AdvancedResourceChecker window = (AdvancedResourceChecker) EditorWindow.GetWindow (typeof (AdvancedResourceChecker));
		window.InitSub();
		window.CheckResources();
		window.minSize=new Vector2(MinWidth,475);
	}

	void InitSub()
	{
		_activeInspectType = (InspectType)PlayerPrefs.GetInt(strActiveInspectType, (int)_activeInspectType);
		_includeDisabledObjects = PlayerPrefs_GetBool(strIncludeDisabledObjects, _includeDisabledObjects);
		_includeSpriteAnimations = PlayerPrefs_GetBool(strIncludeSpriteAnimations, _includeSpriteAnimations);
		_includeScriptReferences = PlayerPrefs_GetBool(strIncludeScriptReferences, _includeScriptReferences);
		_includeGuiElements = PlayerPrefs_GetBool(strIncludeGuiElements, _includeGuiElements);
	}

	void OnGUI ()
	{
		defColor = GUI.color;
		IncludeDisabledObjects = GUILayout.Toggle(IncludeDisabledObjects, "Include disabled objects", GUILayout.Width(300));
		IncludeSpriteAnimations = GUILayout.Toggle(IncludeSpriteAnimations, "Look in sprite animations", GUILayout.Width(300));
		GUI.color = new Color (0.8f, 0.8f, 1.0f, 1.0f);
		IncludeScriptReferences = GUILayout.Toggle(IncludeScriptReferences, "Look in behavior fields", GUILayout.Width(300));
		GUI.color = new Color (1.0f, 0.95f, 0.8f, 1.0f);
		IncludeGuiElements = GUILayout.Toggle(IncludeGuiElements, "Look in GUI elements", GUILayout.Width(300));
		GUI.color = defColor;
		GUILayout.BeginArea(new Rect(position.width-85,5,100,85));
		if (GUILayout.Button("Calculate",GUILayout.Width(80), GUILayout.Height(40)))
			CheckResources();
		if (GUILayout.Button("CleanUp",GUILayout.Width(80), GUILayout.Height(20)))
			Resources.UnloadUnusedAssets();
		if (GUILayout.Button("SelectAll", GUILayout.Width(80), GUILayout.Height(20)))
			selectAll();
		GUILayout.EndArea();
		RemoveDestroyedResources();

		GUILayout.Space(30);
		if (thingsMissing == true) {
			EditorGUI.HelpBox (new Rect(8,75,300,25),"Some GameObjects are missing graphical elements.", MessageType.Error);
		}
		GUILayout.BeginHorizontal();
		GUILayout.Label("Textures "+ActiveTextures.Count+" - "+FormatSizeString(TotalTextureMemory));
		GUILayout.Label("Materials "+ActiveMaterials.Count);
		GUILayout.Label("Shaders  " + shaderCount);
		GUILayout.Label("Meshes "+ActiveMeshDetails.Count+" - "+TotalMeshVertices+" verts");
		GUILayout.Label("AnimationClips " + ActiveAnimationClips.Count + " - " + FormatSizeString(TotalAnimationClipMemory));
		GUILayout.Label("AudioClips " + ActiveAudioClips.Count );
		GUILayout.EndHorizontal();
		if (thingsMissing == true) {
			ActiveInspectType = (InspectType)GUILayout.Toolbar ((int)ActiveInspectType, inspectToolbarStrings2);
		} else {
			ActiveInspectType = (InspectType)GUILayout.Toolbar ((int)ActiveInspectType, inspectToolbarStrings);
		}

		ctrlPressed=Event.current.control || Event.current.command;

		switch (ActiveInspectType)
		{
		case InspectType.Textures:
			ListTextures();
			break;
		case InspectType.Materials:
			ListMaterials();
			break;
		case InspectType.Shaders:
			ListShader();
			break;
		case InspectType.Meshes:
			ListMeshes();
			break;
		case InspectType.AnimationClips:
			ListAnimationClips();
			break;
		case InspectType.AudioClips:
			ListAudioClips();
			break;
		case InspectType.Missing:
			ListMissing();
			break;
		}
	}

	private void RemoveDestroyedResources()
	{
		if (collectedInPlayingMode != Application.isPlaying)
		{
			ActiveTextures.Clear();
			ActiveMaterials.Clear();
			ActiveMeshDetails.Clear();
			ActiveAnimationClips.Clear();
			ActiveAudioClips.Clear();
			MissingObjects.Clear ();
			thingsMissing = false;
			collectedInPlayingMode = Application.isPlaying;
		}
		
		ActiveTextures.RemoveAll(x => !x.texture);
		ActiveTextures.ForEach(delegate(TextureDetails obj) {
			obj.FoundInAnimators.RemoveAll(x => !x);
			obj.FoundInMaterials.RemoveAll(x => !x);
			obj.FoundInRenderers.RemoveAll(x => !x);
			obj.FoundInScripts.RemoveAll(x => !x);
			obj.FoundInGraphics.RemoveAll(x => !x);
		});

		ActiveMaterials.RemoveAll(x => !x.material);
		ActiveMaterials.ForEach(delegate(MaterialDetails obj) {
			obj.FoundInRenderers.RemoveAll(x => !x);
			obj.FoundInGraphics.RemoveAll(x => !x);
		});

		ActiveMeshDetails.RemoveAll(x => !x.mesh);
		ActiveMeshDetails.ForEach(delegate(MeshDetails obj) {
			obj.FoundInMeshFilters.RemoveAll(x => !x);
			obj.FoundInSkinnedMeshRenderer.RemoveAll(x => !x);
		});

		ActiveAnimationClips.RemoveAll(x => !x.animationClip);
		ActiveAnimationClips.ForEach(delegate (AnimationClipDetails obj) {
			obj.FoundInMonoBehaviour.RemoveAll(x => !x);
#if RESOURCECHECKER_TIMELINE
			obj.FoundInPlayableAsset.RemoveAll(x => !x);
#endif
		});

		ActiveAudioClips.RemoveAll(x => !x.audioClip);
		ActiveAudioClips.ForEach(delegate (AudioClipDetails obj) {
			obj.FoundInMonoBehaviour.RemoveAll(x => !x);
#if RESOURCECHECKER_TIMELINE
			obj.FoundInPlayableAsset.RemoveAll(x => !x);
#endif
		});

		TotalTextureMemory = 0;
		foreach (TextureDetails tTextureDetails in ActiveTextures) TotalTextureMemory += tTextureDetails.memSizeKB;

		TotalMeshVertices = 0;
		foreach (MeshDetails tMeshDetails in ActiveMeshDetails) TotalMeshVertices += tMeshDetails.mesh.vertexCount;

		TotalAnimationClipMemory = 0;
		foreach (AnimationClipDetails tAnimationClipDetails in ActiveAnimationClips) TotalAnimationClipMemory += CalcurateAnimationClipSizeKb(tAnimationClipDetails.animationClip);
	}

	int GetBitsPerPixel(TextureFormat format)
	{
		switch (format)
		{
		case TextureFormat.Alpha8: //	 Alpha-only texture format.
			return 8;
		case TextureFormat.ARGB4444: //	 A 16 bits/pixel texture format. Texture stores color with an alpha channel.
			return 16;
		case TextureFormat.RGBA4444: //	 A 16 bits/pixel texture format.
			return 16;
		case TextureFormat.RGB24:	// A color texture format.
			return 24;
		case TextureFormat.RGBA32:	//Color with an alpha channel texture format.
			return 32;
		case TextureFormat.ARGB32:	//Color with an alpha channel texture format.
			return 32;
		case TextureFormat.RGB565:	//	 A 16 bit color texture format.
			return 16;
		case TextureFormat.DXT1:	// Compressed color texture format.
			return 4;
		case TextureFormat.DXT5:	// Compressed color with alpha channel texture format.
			return 8;
			/*
			case TextureFormat.WiiI4:	// Wii texture format.
			case TextureFormat.WiiI8:	// Wii texture format. Intensity 8 bit.
			case TextureFormat.WiiIA4:	// Wii texture format. Intensity + Alpha 8 bit (4 + 4).
			case TextureFormat.WiiIA8:	// Wii texture format. Intensity + Alpha 16 bit (8 + 8).
			case TextureFormat.WiiRGB565:	// Wii texture format. RGB 16 bit (565).
			case TextureFormat.WiiRGB5A3:	// Wii texture format. RGBA 16 bit (4443).
			case TextureFormat.WiiRGBA8:	// Wii texture format. RGBA 32 bit (8888).
			case TextureFormat.WiiCMPR:	//	 Compressed Wii texture format. 4 bits/texel, ~RGB8A1 (Outline alpha is not currently supported).
				return 0;  //Not supported yet
			*/
		case TextureFormat.PVRTC_RGB2://	 PowerVR (iOS) 2 bits/pixel compressed color texture format.
			return 2;
		case TextureFormat.PVRTC_RGBA2://	 PowerVR (iOS) 2 bits/pixel compressed with alpha channel texture format
			return 2;
		case TextureFormat.PVRTC_RGB4://	 PowerVR (iOS) 4 bits/pixel compressed color texture format.
			return 4;
		case TextureFormat.PVRTC_RGBA4://	 PowerVR (iOS) 4 bits/pixel compressed with alpha channel texture format
			return 4;
		case TextureFormat.ETC_RGB4://	 ETC (GLES2.0) 4 bits/pixel compressed RGB texture format.
			return 4;								
		case TextureFormat.BGRA32://	 Format returned by iPhone camera
			return 32;
#if !UNITY_5 && !UNITY_5_3_OR_NEWER
			case TextureFormat.ATF_RGB_DXT1://	 Flash-specific RGB DXT1 compressed color texture format.
			case TextureFormat.ATF_RGBA_JPG://	 Flash-specific RGBA JPG-compressed color texture format.
			case TextureFormat.ATF_RGB_JPG://	 Flash-specific RGB JPG-compressed color texture format.
			return 0; //Not supported yet  
#endif
		}
		return 0;
	}

	int CalculateTextureSizeBytes(Texture tTexture)
	{

		int tWidth=tTexture.width;
		int tHeight=tTexture.height;
		if (tTexture is Texture2D)
		{
			Texture2D tTex2D=tTexture as Texture2D;
			int bitsPerPixel=GetBitsPerPixel(tTex2D.format);
			int mipMapCount=tTex2D.mipmapCount;
			int mipLevel=1;
			int tSize=0;
			while (mipLevel<=mipMapCount)
			{
				tSize+=tWidth*tHeight*bitsPerPixel/8;
				tWidth=tWidth/2;
				tHeight=tHeight/2;
				mipLevel++;
			}
			return tSize;
		}
		if (tTexture is Texture2DArray)
		{
			Texture2DArray tTex2D=tTexture as Texture2DArray;
			int bitsPerPixel=GetBitsPerPixel(tTex2D.format);
			int mipMapCount=10;
			int mipLevel=1;
			int tSize=0;
			while (mipLevel<=mipMapCount)
			{
				tSize+=tWidth*tHeight*bitsPerPixel/8;
				tWidth=tWidth/2;
				tHeight=tHeight/2;
				mipLevel++;
			}
			return tSize*((Texture2DArray)tTex2D).depth;
		}
		if (tTexture is Cubemap) {
			Cubemap tCubemap = tTexture as Cubemap;
			int bitsPerPixel = GetBitsPerPixel (tCubemap.format);
			return tWidth * tHeight * 6 * bitsPerPixel / 8;
		}
		return 0;
	}



	void SelectObject(Object selectedObject,bool append)
	{
		if (append)
		{
			List<Object> currentSelection=new List<Object>(Selection.objects);
			// Allow toggle selection
			if (currentSelection.Contains(selectedObject)) currentSelection.Remove(selectedObject);
			else currentSelection.Add(selectedObject);

			Selection.objects=currentSelection.ToArray();
		}
		else Selection.activeObject=selectedObject;
	}

	void SelectObjects(List<Object> selectedObjects,bool append)
	{
		if (append)
		{
			List<Object> currentSelection=new List<Object>(Selection.objects);
			currentSelection.AddRange(selectedObjects);
			Selection.objects=currentSelection.ToArray();
		}
		else Selection.objects=selectedObjects.ToArray();
	}

	void SelectMaterials(List<Material> selectedMaterials, bool append)
	{
		if (append)
		{
			List<Material> currentSelection = new List<Material>();
			currentSelection.AddRange(selectedMaterials);
			Selection.objects = currentSelection.ToArray();
		}
		else Selection.objects = selectedMaterials.ToArray();
	}

	void ListTextures()
	{
		textureListScrollPos = EditorGUILayout.BeginScrollView(textureListScrollPos);
		List<Object> MipMapTextures = new List<Object>();
		List<Object> SuperLargeTextures = new List<Object>(); //Store Over 2048 Textures
		List<Object> ExtraLargeTextures = new List<Object>(); //Store 2048 Textures
		List<Object> LargeTextures = new List<Object>(); //Store 1024 Textures
		List<Object> MediumTextures = new List<Object>(); //Store 512 Textures
		List<Object> SmallTextures = new List<Object>(); //Store Below 512 Textures

		EditorGUI.BeginDisabledGroup(ActiveTextures.Count == 0);
		GUILayout.BeginHorizontal();
		if (GUILayout.Button("Sort By Name", GUILayout.Width(200)))
		{
			SortTextureName();
		}

		if (GUILayout.Button("Sort By Format", GUILayout.Width(200)))
		{
			SortTextureFormat();
		}

		if (GUILayout.Button("Sort By Size", GUILayout.Width(200)))
		{
			SortTextureSize();
		}

		if (GUILayout.Button("Sort By Alpha", GUILayout.Width(200)))
		{
			SortTextureAlpha();
		}

		GUILayout.EndHorizontal();

		{
			EditorGUILayout.Space();
			GUILayout.BeginHorizontal();
			//GUILayout.Box(" ",GUILayout.Width(ThumbnailWidth),GUILayout.Height(ThumbnailHeight));
			if (GUILayout.Button("Select All", GUILayout.Width(100)))
			{
				List<Object> AllTextures = new List<Object>();
				foreach (TextureDetails tDetails in ActiveTextures) AllTextures.Add(tDetails.texture);
				SelectObjects(AllTextures, ctrlPressed);
			}

			if (GUILayout.Button("Select 2048+", GUILayout.Width(100)))
			{
				SelectObjects(SuperLargeTextures, ctrlPressed);
			}
			if (GUILayout.Button("Select 2048", GUILayout.Width(100)))
			{
				SelectObjects(ExtraLargeTextures, ctrlPressed);
			}
			if (GUILayout.Button("Select 1024", GUILayout.Width(100)))
			{
				SelectObjects(LargeTextures, ctrlPressed);
			}
			if (GUILayout.Button("Select 512", GUILayout.Width(100)))
			{
				SelectObjects(MediumTextures, ctrlPressed);
			}
			if (GUILayout.Button("Select 512-", GUILayout.Width(100)))
			{
				SelectObjects(SmallTextures, ctrlPressed);
			}
			EditorGUILayout.EndHorizontal();
		}

		var activeTextures = ActiveTextures;
		FilterTextField(onProcessFilter: (filterText) => activeTextures = ActiveTextures.Where(x => Wildcard(x.texture.name, filterText, _ignoreCase)).ToList());
		EditorGUI.EndDisabledGroup();

		int startIndex = 0;
		int lastIndex = 0;
		if (activeTextures.Count > 0)
		{
			int pageCount = (activeTextures.Count + PageDiv - 1) / PageDiv;
			PageButtons(ref _currentTexturePage, pageCount);
			startIndex = _currentTexturePage * PageDiv;
			lastIndex = Mathf.Min(startIndex + PageDiv, activeTextures.Count);
		}
		for (int i = startIndex; i < lastIndex; i++)
		{
			TextureDetails tDetails = activeTextures[i];

			GUILayout.BeginHorizontal ();
			
			Texture tex =tDetails.texture;			
			if(tDetails.texture.GetType() == typeof(Texture2DArray) || tDetails.texture.GetType() == typeof(Cubemap)){
				tex = AssetPreview.GetMiniThumbnail(tDetails.texture);
			}
			GUILayout.Box(tex, GUILayout.Width(ThumbnailWidth), GUILayout.Height(ThumbnailHeight));

			if (tDetails.instance == true)
				GUI.color = new Color (0.8f, 0.8f, defColor.b, 1.0f);
			if (tDetails.isgui == true)
				GUI.color = new Color (defColor.r, 0.95f, 0.8f, 1.0f);
			if (tDetails.isSky)
				GUI.color = new Color (0.9f, defColor.g, defColor.b, 1.0f);

			GUILayout.BeginVertical();

			if (GUILayout.Button(tDetails.texture.name,GUILayout.Width(158)))
			{
				SelectObject(tDetails.texture,ctrlPressed);
			}


			GUILayout.BeginHorizontal();
			GUILayout.Label("Uses- ", GUILayout.Width(35));
			EditorGUI.BeginDisabledGroup(tDetails.FoundInMaterials.Count == 0);
			if (GUILayout.Button(tDetails.FoundInMaterials.Count + " Mats", GUILayout.Width(55)))
			{
				SelectObjects(tDetails.FoundInMaterials, ctrlPressed);
			}
			EditorGUI.EndDisabledGroup();

			HashSet<Object> FoundObjects = new HashSet<Object>();
			foreach (Renderer renderer in tDetails.FoundInRenderers) FoundObjects.Add(renderer.gameObject);
			foreach (Animator animator in tDetails.FoundInAnimators) FoundObjects.Add(animator.gameObject);
			foreach (Graphic graphic in tDetails.FoundInGraphics) FoundObjects.Add(graphic.gameObject);
			foreach (Button button in tDetails.FoundInButtons) FoundObjects.Add(button.gameObject);
			foreach (MonoBehaviour script in tDetails.FoundInScripts) FoundObjects.Add(script.gameObject);
			EditorGUI.BeginDisabledGroup(FoundObjects.Count == 0);
			if (GUILayout.Button(FoundObjects.Count + " GOs", GUILayout.Width(60)))
			{
				SelectObjects(new List<Object>(FoundObjects), ctrlPressed);
			}
			EditorGUI.EndDisabledGroup();
			GUILayout.EndHorizontal();
			GUILayout.Label("Texture Import Setting: ", GUILayout.Width(150));
			GUILayout.EndVertical();

			TextureImporter importer = AssetImporter.GetAtPath(AssetDatabase.GetAssetPath(tDetails.texture)) as TextureImporter;
			string mobileFormat = "";
			int maxSize = 0;
			string sizeLabel = "";

			if (importer != null)
			{
				TextureImporterPlatformSettings androidSettings = importer.GetPlatformTextureSettings("Android");
				TextureImporterPlatformSettings iOSSettings = importer.GetPlatformTextureSettings("iPhone");


				string androidfileformat = "Android Max Size: " + androidSettings.maxTextureSize + "  Format:  " + androidSettings.format;

				if (!androidSettings.overridden)
				{
					androidfileformat = "Android: Same as Default ";
				}

				string iOSfileformat = "iPhone Max Size: " + iOSSettings.maxTextureSize + "  Format:  " + iOSSettings.format;

				if (!iOSSettings.overridden)
				{
					iOSfileformat = "iPhone: Same as Default";
				}
				mobileFormat = androidfileformat + "\n" + iOSfileformat;
				maxSize = importer.maxTextureSize;
				sizeLabel = "Change Max Size (Default: " + maxSize + ")";
			}



			if (importer != null && importer.textureType == TextureImporterType.Default)
			{
				if (importer.alphaSource == TextureImporterAlphaSource.FromInput && importer.DoesSourceTextureHaveAlpha())
				{
					tDetails.hasAlpha = true;
				}
				else
				{
					tDetails.hasAlpha = false;
				}

				if (importer.DoesSourceTextureHaveAlpha())
				{
					if(importer.alphaSource != TextureImporterAlphaSource.FromInput)
					{
						tDetails.hasAlphaWrongSetting = true;
					}
				}
			}
	  
			string alphaCheckBoolean = tDetails.hasAlpha ? "+ Alpha" : "";


			GUI.color = defColor;
			string textureAssetPath = AssetDatabase.GetAssetPath(tex);
			string fileformat;
			string cubemapDetail;
			bool streamingSettingCheck = false;
			bool generateMipmap = false;
			bool readWriteBoolean = false;







			if (importer != null && importer.textureType == TextureImporterType.NormalMap)
			{
				fileformat = Path.GetExtension(textureAssetPath).ToUpper().TrimStart('.') + " - Normal Map";
				fileformat += "\n" + FormatSizeString(tDetails.memSizeKB) + " - " + tDetails.format;
			}
			else
			{
				fileformat = Path.GetExtension(textureAssetPath).ToUpper().TrimStart('.') + " " + alphaCheckBoolean;
				fileformat += "\n" + FormatSizeString(tDetails.memSizeKB) + " - " + tDetails.format;
			}



			TextureImporter textureImporterSetting = AssetImporter.GetAtPath(textureAssetPath) as TextureImporter;

			cubemapDetail = "Size: (" + tDetails.texture.width + " x" + tDetails.texture.height;
			cubemapDetail += ")\n Cubemap  " + "\n" + FormatSizeString(tDetails.memSizeKB) + " - " + tDetails.format;



			if (tDetails.isCubeMap)
			{
				GUILayout.Label(cubemapDetail, GUILayout.Width(150));
			}
			else
			{
				GUILayout.BeginVertical();
				GUILayout.Label(fileformat, GUILayout.Width(150));
				if (textureImporterSetting != null)
				{
					readWriteBoolean = textureImporterSetting.isReadable;
					string readWriteBoolString = readWriteBoolean ? "O" : "X";

					if (GUILayout.Button("Read Write: " + readWriteBoolString, GUILayout.Width(130)))
					{
						textureImporterSetting.isReadable = !textureImporterSetting.isReadable;
						AssetDatabase.ImportAsset(textureAssetPath, ImportAssetOptions.ForceUpdate);
					}

				}
				GUILayout.EndVertical();
			}



			if (importer != null)
			{
				GUILayout.BeginVertical();
				GUILayout.Label("Texture Size: (" + tDetails.texture.width + " x" + tDetails.texture.height + ")");
				GUILayout.Label(sizeLabel, GUILayout.Width(200));
				GUILayout.BeginHorizontal();
				GUILayout.Label("-", GUILayout.Width(10));
				if (GUILayout.Button("2048", GUILayout.Width(45)))
				{
					textureImporterSetting.maxTextureSize = 2048;
					AssetDatabase.ImportAsset(textureAssetPath, ImportAssetOptions.ForceUpdate);
				}
				if (GUILayout.Button("1024", GUILayout.Width(40)))
				{
					textureImporterSetting.maxTextureSize = 1024;
					AssetDatabase.ImportAsset(textureAssetPath, ImportAssetOptions.ForceUpdate);
				}

				if (GUILayout.Button("512", GUILayout.Width(35)))
				{
					textureImporterSetting.maxTextureSize = 512;
					AssetDatabase.ImportAsset(textureAssetPath, ImportAssetOptions.ForceUpdate);
				}
				if (GUILayout.Button("256", GUILayout.Width(35)))
				{
					textureImporterSetting.maxTextureSize = 256;
					AssetDatabase.ImportAsset(textureAssetPath, ImportAssetOptions.ForceUpdate);
				}
				GUILayout.Label("-", GUILayout.Width(10));
				GUILayout.EndHorizontal();
				GUILayout.EndVertical();




				if (textureImporterSetting != null)
				{
					GUILayout.BeginVertical();
					GUILayout.Label(mobileFormat, GUILayout.Width(300));
					GUILayout.BeginHorizontal();
					streamingSettingCheck = textureImporterSetting.streamingMipmaps;
					generateMipmap = textureImporterSetting.mipmapEnabled;
					string generateMipMapBoolean = generateMipmap ? "O" : "X";
					string streamingSettingCheckBoolean = streamingSettingCheck ? "O" : "X";
					string streamingSetting = " " + streamingSettingCheckBoolean;
					if (tDetails.hasAlphaWrongSetting)
					{
						if (GUILayout.Button("Fix Alpha Setting: " + " Input Texture Alpha", GUILayout.Width(260)))
						{
							// Set alpha source to input texture alpha
							textureImporterSetting.alphaSource = TextureImporterAlphaSource.FromInput;
							AssetDatabase.ImportAsset(textureAssetPath, ImportAssetOptions.ForceUpdate);
							CheckResources();
						}
					}
					else
					{


						if (GUILayout.Button("Generate Mipmap: " + generateMipMapBoolean, GUILayout.Width(130)))
						{
							textureImporterSetting.mipmapEnabled = !textureImporterSetting.mipmapEnabled;
							AssetDatabase.ImportAsset(textureAssetPath, ImportAssetOptions.ForceUpdate);
						}

						if (GUILayout.Button("Stream Mipmap: " + streamingSetting, GUILayout.Width(130)))
						{
							textureImporterSetting.streamingMipmaps = !textureImporterSetting.streamingMipmaps;
							AssetDatabase.ImportAsset(textureAssetPath, ImportAssetOptions.ForceUpdate);
						}
					}


					if (tDetails.texture.width > 2048)
						SuperLargeTextures.Add(tDetails.texture);

					if (tDetails.texture.width == 2048)
						ExtraLargeTextures.Add(tDetails.texture);

					if (tDetails.texture.width == 1024)
						LargeTextures.Add(tDetails.texture);

					if (tDetails.texture.width == 512)
						MediumTextures.Add(tDetails.texture);

					if (tDetails.texture.width < 512)
						SmallTextures.Add(tDetails.texture);
					GUILayout.EndHorizontal();
					GUILayout.EndVertical();

				}

				else if (!tDetails.isCubeMap)
				{
					GUILayout.BeginHorizontal();
					GUILayout.Label("Import Setting N/A", GUILayout.Width(260));
					GUILayout.EndHorizontal();
				}
				else
				{
					GUILayout.BeginHorizontal();
					GUILayout.Label("Import Setting: N/A", GUILayout.Width(260));
					GUILayout.EndHorizontal();
				}
			}
			else
			{
				GUILayout.BeginHorizontal();
				GUILayout.Label("\n\n\nTexture Import Setting N/A", GUILayout.Width(300));
				GUILayout.EndHorizontal();
			}
			

			


	   

			GUILayout.EndHorizontal();

		}


		EditorGUILayout.Space();
		EditorGUI.BeginDisabledGroup(activeTextures.Count == 0);
		GUILayout.BeginHorizontal();

		if (GUILayout.Button("Turn on Mipmap Stream", GUILayout.Width(250)))
		{
			foreach (TextureDetails tDetails in activeTextures)
			{
				Texture tex = tDetails.texture;
				string textureAssetPath = AssetDatabase.GetAssetPath(tex);
				TextureImporter textureImporterSetting = AssetImporter.GetAtPath(textureAssetPath) as TextureImporter;
				if (textureImporterSetting != null)
				{
					if (!textureImporterSetting.streamingMipmaps)
					{
						textureImporterSetting.streamingMipmaps = true;
						AssetDatabase.ImportAsset(textureAssetPath, ImportAssetOptions.ForceUpdate);
					}
				}
			}
		}


		if (GUILayout.Button("Turn off Mipmap Stream", GUILayout.Width(250)))
		{
			foreach (TextureDetails tDetails in activeTextures)
			{
				Texture tex = tDetails.texture;
				string textureAssetPath = AssetDatabase.GetAssetPath(tex);
				TextureImporter textureImporterSetting = AssetImporter.GetAtPath(textureAssetPath) as TextureImporter;
				if (textureImporterSetting != null)
				{
					if (textureImporterSetting.streamingMipmaps)
					{
						textureImporterSetting.streamingMipmaps = false;
						AssetDatabase.ImportAsset(textureAssetPath, ImportAssetOptions.ForceUpdate);
					}
				}
			}
		}

		EditorGUILayout.EndHorizontal();
		EditorGUI.EndDisabledGroup();

		EditorGUILayout.EndScrollView();
	}

	void selectAll()
	{
		switch (ActiveInspectType)
		{
			case InspectType.Textures:
				if (ActiveTextures.Count > 0)
				{
						List<Object> AllTextures = new List<Object>();
						foreach (TextureDetails tDetails in ActiveTextures) AllTextures.Add(tDetails.texture);
						SelectObjects(AllTextures, ctrlPressed);
					
				}
				break;
			case InspectType.Materials:
				ListMaterials();
				break;
			case InspectType.Meshes:
				ListMeshes();
				break;
			case InspectType.Missing:
				ListMissing();
				break;
		}
	}

	void ListMaterials()
	{
		materialListScrollPos = EditorGUILayout.BeginScrollView(materialListScrollPos);

		EditorGUI.BeginDisabledGroup(ActiveMaterials.Count == 0);

		GUILayout.BeginHorizontal();
		if (GUILayout.Button("Sort By Shader", GUILayout.Width(200)))
		{
			SortMaterialShaderBrand();
			SortMaterialShader();
		}



		if (GUILayout.Button("Sort By Material", GUILayout.Width(200)))
		{
			SortMaterialName();
		}
		EditorGUILayout.EndHorizontal();

		var activeMaterials = ActiveMaterials;
		FilterTextField(onProcessFilter: (filterText) => activeMaterials = ActiveMaterials.Where(x => Wildcard(x.material.name, filterText, _ignoreCase)).ToList());
		EditorGUI.EndDisabledGroup();

		int startIndex = 0;
		int lastIndex = 0;
		if (activeMaterials.Count > 0)
		{
			int pageCount = (activeMaterials.Count + PageDiv - 1) / PageDiv;
			PageButtons(ref _currentMaterialPage, pageCount);
			startIndex = _currentMaterialPage * PageDiv;
			lastIndex = Mathf.Min(startIndex + PageDiv, activeMaterials.Count);
		}
		for (int i = startIndex; i < lastIndex; i++)
		{
			MaterialDetails tDetails = activeMaterials[i];

			if (tDetails.material != null)
			{
				GUILayout.BeginHorizontal();

				GUILayout.Box(AssetPreview.GetAssetPreview(tDetails.material), GUILayout.Width(ThumbnailWidth), GUILayout.Height(ThumbnailHeight));

				if (tDetails.instance == true)
					GUI.color = new Color(0.8f, 0.8f, defColor.b, 1.0f);
				if (tDetails.isgui == true)
					GUI.color = new Color(defColor.r, 0.95f, 0.8f, 1.0f);
				if (tDetails.isSky)
					GUI.color = new Color(0.9f, defColor.g, defColor.b, 1.0f);
				if (GUILayout.Button(tDetails.material.name, GUILayout.Width(150)))
				{
					SelectObject(tDetails.material, ctrlPressed);
				}
				GUI.color = defColor;

				string shaderLabel = tDetails.material.shader != null ? tDetails.material.shader.name : "no shader";

				string shaderShort = GetShaderName(shaderLabel);

				string shaderOrigin = GetShaderOrigin(shaderLabel, '/');


				tDetails.shaderName = shaderShort;
				tDetails.shaderBrand = shaderOrigin;

				GUILayout.Label(shaderOrigin, GUILayout.Width(70));
				GUILayout.Label(shaderShort, GUILayout.Width(170));
				string GPUInstancingBoolean = tDetails.material.enableInstancing ? "O" : "X";

				if (GUILayout.Button("GPU Instancing:   " + GPUInstancingBoolean, GUILayout.Width(150)))
				{
					tDetails.material.enableInstancing = !tDetails.material.enableInstancing;
				}
				GUILayout.Label(" ", GUILayout.Width(20));
				EditorGUI.BeginDisabledGroup((tDetails.FoundInRenderers.Count + tDetails.FoundInGraphics.Count) == 0);
				if (GUILayout.Button((tDetails.FoundInRenderers.Count + tDetails.FoundInGraphics.Count) + " GO", GUILayout.Width(50)))
				{
					List<Object> FoundObjects = new List<Object>();
					foreach (Renderer renderer in tDetails.FoundInRenderers) FoundObjects.Add(renderer.gameObject);
					foreach (Graphic graphic in tDetails.FoundInGraphics) FoundObjects.Add(graphic.gameObject);
					SelectObjects(FoundObjects, ctrlPressed);
				}
				EditorGUI.EndDisabledGroup();

				GUILayout.Label("Render Queue: " + tDetails.material.renderQueue.ToString());

				if (tDetails.material != null)
				{
					EditorGUI.BeginDisabledGroup(tDetails.validKeywords.Length == 0);
					if (GUILayout.Button(tDetails.validKeywords.Length.ToString() + " Valid Keywords", GUILayout.Width(150)))
					{
						var rect = new Rect(Event.current.mousePosition, Vector2.one);
						rect.y += EditorGUIUtility.singleLineHeight / 2;
						PopupWindow.Show(rect, new DropdownWindowContext(tDetails.validKeywords));
					}
					EditorGUI.EndDisabledGroup();

					EditorGUI.BeginDisabledGroup(tDetails.invalidKeywords.Length == 0);
					if (GUILayout.Button(tDetails.invalidKeywords.Length.ToString() + " Invalid Keywords", GUILayout.Width(150)))
					{
						var rect = new Rect(Event.current.mousePosition, Vector2.one);
						rect.y += EditorGUIUtility.singleLineHeight / 2;
						PopupWindow.Show(rect, new DropdownWindowContext(tDetails.invalidKeywords));
					}
					EditorGUI.EndDisabledGroup();
				}

				GUILayout.EndHorizontal();
			}
		}

		EditorGUILayout.EndScrollView();
	}
	void ListShader()
	{
		shaderListScrollPos = EditorGUILayout.BeginScrollView(shaderListScrollPos);
		printedMaterials = new HashSet<string>();

		EditorGUI.BeginDisabledGroup(ActiveMaterials.Count == 0);
		var activeMaterials = ActiveMaterials;
		FilterTextField(onProcessFilter: (filterText) => activeMaterials = ActiveMaterials.Where(x => Wildcard(x.material.shader.name, filterText, _ignoreCase)).ToList());
		EditorGUI.EndDisabledGroup();

		foreach (MaterialDetails tDetails in activeMaterials)
		{
			int count = 0;
			if (tDetails.material != null)
			{
				GUILayout.BeginHorizontal();

				tDetails.shaderName = tDetails.material.shader.name;
				List<Material> FoundMaterials = new List<Material>();


				foreach (MaterialDetails material in activeMaterials)
				{
					if (material.material.shader == tDetails.material.shader)
					{
						FoundMaterials.Add(material.material);
						count++;
					}
				}


				if (!printedMaterials.Contains(tDetails.shaderName))
				{
					printedMaterials.Add(tDetails.shaderName);
					if (GUILayout.Button(count.ToString() + " Materials", GUILayout.Width(100)))
					{
						SelectMaterials(FoundMaterials, ctrlPressed);
					}
					GUILayout.Label( " uses  ", GUILayout.Width(50));
					if (GUILayout.Button(GetShaderName(tDetails.shaderName), GUILayout.Width(250)))
					{
						SelectMaterials(FoundMaterials, ctrlPressed);
						{
							Shader shader = tDetails.material.shader;
							EditorGUIUtility.PingObject(shader);
							Selection.activeObject = shader;
						}
					}
				}



				GUILayout.EndHorizontal();
			}

		}

		EditorGUILayout.EndScrollView();
	}


	public static string GetShaderName(string fullShaderName)
	{
		int lastSlashIndex = fullShaderName.LastIndexOf('/');
		if (lastSlashIndex >= 0 && lastSlashIndex < fullShaderName.Length - 1)
		{
			// Extract the shader name after the last slash
			return fullShaderName.Substring(lastSlashIndex + 1);
		}
		else
		{
			// No slash found or it is the last character
			return fullShaderName;
		}
	}
	public static string GetShaderOrigin(string fullShaderName, char character)
	{

			int index = fullShaderName.IndexOf(character);
			if (index >= 0)
			{
				// Extract the substring before the character
				return fullShaderName.Substring(0, index);
			}
			else
			{
				// Character not found, return the original string
				return fullShaderName;
			}
		
	}

	void ListMeshes()
	{
		meshListScrollPos = EditorGUILayout.BeginScrollView(meshListScrollPos);

		EditorGUI.BeginDisabledGroup(ActiveMeshDetails.Count == 0);
		var activeMeshDetails = ActiveMeshDetails;
		FilterTextField(onProcessFilter: (filterText) => activeMeshDetails = ActiveMeshDetails.Where(x => Wildcard(x.mesh.name, filterText, _ignoreCase)).ToList());
		EditorGUI.EndDisabledGroup();
		int startIndex = 0;
		int lastIndex = 0;
		if (activeMeshDetails.Count > 0)
		{
			int pageCount = (activeMeshDetails.Count + PageDiv - 1) / PageDiv;
			PageButtons(ref _currentMeshPage, pageCount);
			startIndex = _currentMeshPage * PageDiv;
			lastIndex = Mathf.Min(startIndex + PageDiv, activeMeshDetails.Count);
		}
		for (int i = startIndex; i < lastIndex; i++)
		{
			MeshDetails tDetails = activeMeshDetails[i];

			if (tDetails.mesh!=null)
			{
				GUILayout.BeginHorizontal ();
				string name = tDetails.mesh.name;
				if (name == null || name.Count() < 1)
					name = tDetails.FoundInMeshFilters[0].gameObject.name;
				if (tDetails.instance == true)
					GUI.color = new Color (0.8f, 0.8f, defColor.b, 1.0f);
				if(GUILayout.Button(name,GUILayout.Width(150)))
				{
					SelectObject(tDetails.mesh,ctrlPressed);
				}
				GUI.color = defColor;
				string sizeLabel=""+tDetails.mesh.vertexCount+" vert";

				GUILayout.Label (sizeLabel,GUILayout.Width(100));

				string IsFBX = CheckIfFromFBX(tDetails.mesh) ? "FBX" : "Not FBX";
				GUILayout.Label("  " + IsFBX, GUILayout.Width(100));

				EditorGUI.BeginDisabledGroup(tDetails.FoundInMeshFilters.Count == 0);
				if (GUILayout.Button(tDetails.FoundInMeshFilters.Count + " GO",GUILayout.Width(50)))
				{
					List<Object> FoundObjects=new List<Object>();
					foreach (MeshFilter meshFilter in tDetails.FoundInMeshFilters) FoundObjects.Add(meshFilter.gameObject);
					SelectObjects(FoundObjects,ctrlPressed);
				}
				EditorGUI.EndDisabledGroup();

				GUILayout.Label("Export as ", GUILayout.Width(60));
#if !RESOURCECHECKER_EXPORT_FBX
				EditorGUI.BeginDisabledGroup(true);
#endif
				if (GUILayout.Button("FBX", GUILayout.Width(35)))
				{
#if RESOURCECHECKER_EXPORT_FBX
#if UNITY_EDITOR
					Mesh mesh = tDetails.mesh;
						if (mesh != null)
						{
							string meshPath = AssetDatabase.GetAssetPath(mesh);
							string folderPath = System.IO.Path.GetDirectoryName(meshPath);
							string meshName = mesh.name;
							string exportPath = folderPath + "/" + meshName + ".fbx";
							ModelExporter.ExportObject(exportPath, CreateTemporaryObjectWithMesh(mesh));
						}
#else
					Debug.LogError("FBX Exporter plugin is required to export as FBX.");
#endif
#endif
				}
#if !RESOURCECHECKER_EXPORT_FBX
				EditorGUI.EndDisabledGroup();
#endif

				if (tDetails.FoundInSkinnedMeshRenderer.Count > 0) {
					if (GUILayout.Button (tDetails.FoundInSkinnedMeshRenderer.Count + " skinned mesh GO", GUILayout.Width (140))) {
						List<Object> FoundObjects = new List<Object> ();
						foreach (SkinnedMeshRenderer skinnedMeshRenderer in tDetails.FoundInSkinnedMeshRenderer)
							FoundObjects.Add (skinnedMeshRenderer.gameObject);
						SelectObjects (FoundObjects, ctrlPressed);
					}
				} else {
					GUI.color = new Color (defColor.r, defColor.g, defColor.b, 0.5f);
					GUILayout.Label("   0 skinned mesh");
					GUI.color = defColor;
				}





				GUILayout.EndHorizontal();	
			}
		}
		EditorGUILayout.EndScrollView();		
	}
	public static bool IsPartOfPrefab(GameObject gameObject)
	{
		PrefabAssetType prefabType = PrefabUtility.GetPrefabAssetType(gameObject);
		return prefabType != PrefabAssetType.NotAPrefab;
	}


	public bool CheckIfFromFBX(Mesh mesh)
	{
			string assetPath = UnityEditor.AssetDatabase.GetAssetPath(mesh);
			string fileExtension = System.IO.Path.GetExtension(assetPath);

			if (fileExtension == ".fbx")
			{
				return true;
			}
			else
			{
				return false;
			}
	}

	private static GameObject CreateTemporaryObjectWithMesh(Mesh mesh)
	{
		GameObject tempObject = new GameObject("TempObject");
		MeshFilter meshFilter = tempObject.AddComponent<MeshFilter>();
		MeshRenderer meshRenderer = tempObject.AddComponent<MeshRenderer>();

		meshFilter.sharedMesh = mesh;

		return tempObject;
	}

	void ListAnimationClips()
	{
		animationClipListScrollPos = EditorGUILayout.BeginScrollView(animationClipListScrollPos);

		EditorGUI.BeginDisabledGroup(ActiveAnimationClips.Count == 0);
		var activeAnimationClips = ActiveAnimationClips;
		FilterTextField(onProcessFilter: (filterText) => activeAnimationClips = ActiveAnimationClips.Where(x => Wildcard(x.animationClip.name, filterText, _ignoreCase)).ToList());
		EditorGUI.EndDisabledGroup();

		int startIndex = 0;
		int lastIndex = 0;
		if (activeAnimationClips.Count > 0)
		{
			int pageCount = (activeAnimationClips.Count + PageDiv - 1) / PageDiv;
			PageButtons(ref _currentAnimationClipPage, pageCount);
			startIndex = _currentAnimationClipPage * PageDiv;
			lastIndex = Mathf.Min(startIndex + PageDiv, activeAnimationClips.Count);
		}
		for (int i = startIndex; i < lastIndex; i++)
		{
			AnimationClipDetails tDetails = activeAnimationClips[i];
			if (tDetails.animationClip != null)
			{
				GUILayout.BeginHorizontal();
				string name = tDetails.animationClip.name;
				if (name == null || name.Count() < 1)
				{
					if (tDetails.FoundInPlayableAsset.Count > 0)
						name = tDetails.FoundInPlayableAsset[0].name;
					else if (tDetails.FoundInMonoBehaviour.Count > 0)
						name = tDetails.FoundInMonoBehaviour[0].gameObject.name;
				}
				if (tDetails.instance == true)
					GUI.color = new Color(0.8f, 0.8f, defColor.b, 1.0f);
				if (GUILayout.Button(name, GUILayout.Width(250)))
				{
					SelectObject(tDetails.animationClip, ctrlPressed);
				}
				GUI.color = defColor;
				int sizeMemoryKb = CalcurateAnimationClipSizeKb(tDetails.animationClip);
				string sizeLabel = $"";
				sizeLabel += FormatSizeString(sizeMemoryKb);

				GUILayout.Label(sizeLabel, GUILayout.Width(90));

				EditorGUI.BeginDisabledGroup(tDetails.FoundInMonoBehaviour.Count == 0);
				if (GUILayout.Button(tDetails.FoundInMonoBehaviour.Count + " GO", GUILayout.Width(50)))
				{
					HashSet<Object> FoundObjects = new HashSet<Object>();
					foreach (MonoBehaviour monoBehaviour in tDetails.FoundInMonoBehaviour) FoundObjects.Add(monoBehaviour.gameObject);
					SelectObjects(new List<Object>(FoundObjects), ctrlPressed);
				}
				EditorGUI.EndDisabledGroup();
				EditorGUI.BeginDisabledGroup(tDetails.FoundInPlayableAsset.Count == 0);
				if (GUILayout.Button(tDetails.FoundInPlayableAsset.Count + " PA", GUILayout.Width(50)))
				{
					HashSet<Object> FoundObjects = new HashSet<Object>();
					foreach (PlayableAsset playableAsset in tDetails.FoundInPlayableAsset) FoundObjects.Add(playableAsset);
					SelectObjects(new List<Object>(FoundObjects), ctrlPressed);
				}
				EditorGUI.EndDisabledGroup();

				GUILayout.EndHorizontal();
			}
		}
		EditorGUILayout.EndScrollView();
	}

	void ListAudioClips()
	{
		audioClipListScrollPos = EditorGUILayout.BeginScrollView(audioClipListScrollPos);

		EditorGUI.BeginDisabledGroup(ActiveAudioClips.Count == 0);
		var activeAudioClips = ActiveAudioClips;
		FilterTextField(onProcessFilter: (filterText) => activeAudioClips = ActiveAudioClips.Where(x => Wildcard(x.audioClip.name, filterText, _ignoreCase)).ToList());
		EditorGUI.EndDisabledGroup();

		int startIndex = 0;
		int lastIndex = 0;
		if (activeAudioClips.Count > 0)
		{
			int pageCount = (activeAudioClips.Count + PageDiv - 1) / PageDiv;
			PageButtons(ref _currentAudioClipPage, pageCount);
			startIndex = _currentAudioClipPage * PageDiv;
			lastIndex = Mathf.Min(startIndex + PageDiv, activeAudioClips.Count);
		}
		for (int i = startIndex; i < lastIndex; i++)
		{
			AudioClipDetails tDetails = activeAudioClips[i];
			if (tDetails.audioClip != null)
			{
				GUILayout.BeginHorizontal();
				string name = tDetails.audioClip.name;
				if (name == null || name.Count() < 1)
					name = tDetails.FoundInMonoBehaviour[0].gameObject.name;
				if (GUILayout.Button(name, GUILayout.Width(250)))
				{
					SelectObject(tDetails.audioClip, ctrlPressed);
				}
				GUI.color = defColor;
				string sizeLabel = $"{tDetails.audioClip.samples} samples {tDetails.audioClip.channels} channels\n";
				sizeLabel += $"{tDetails.audioClip.loadType}";

				GUILayout.Label(sizeLabel, GUILayout.Width(230));

				EditorGUI.BeginDisabledGroup(tDetails.FoundInMonoBehaviour.Count == 0);
				if (GUILayout.Button(tDetails.FoundInMonoBehaviour.Count + " GO", GUILayout.Width(50)))
				{
					List<Object> FoundObjects = new List<Object>();
					foreach (MonoBehaviour monoBehaviour in tDetails.FoundInMonoBehaviour) FoundObjects.Add(monoBehaviour.gameObject);
					SelectObjects(FoundObjects, ctrlPressed);
				}
				EditorGUI.EndDisabledGroup();
				EditorGUI.BeginDisabledGroup(tDetails.FoundInPlayableAsset.Count == 0);
				if (GUILayout.Button(tDetails.FoundInPlayableAsset.Count + " PA", GUILayout.Width(50)))
				{
					HashSet<Object> FoundObjects = new HashSet<Object>();
					foreach (PlayableAsset playableAsset in tDetails.FoundInPlayableAsset) FoundObjects.Add(playableAsset);
					SelectObjects(new List<Object>(FoundObjects), ctrlPressed);
				}
				EditorGUI.EndDisabledGroup();

				GUILayout.EndHorizontal();
			}
		}
		EditorGUILayout.EndScrollView();
	}

	void ListMissing(){
		missingListScrollPos = EditorGUILayout.BeginScrollView(missingListScrollPos);
		foreach (MissingGraphic dMissing in MissingObjects) {
			GUILayout.BeginHorizontal ();
			if (GUILayout.Button (dMissing.name, GUILayout.Width (150)))
				SelectObject (dMissing.Object, ctrlPressed);
			GUILayout.Label ("missing ", GUILayout.Width(48));
			switch (dMissing.type) {
			case "lod":
				GUI.color = new Color(defColor.r, defColor.b, 0.8f, 1.0f);
				break;
			case "mesh":
				GUI.color = new Color (0.8f, 0.8f, defColor.b, 1.0f);
				break;
			case "sprite":
				GUI.color = new Color (defColor.r, 0.8f, 0.8f, 1.0f);
				break;
			case "material":
				GUI.color = new Color (0.8f, defColor.g, 0.8f, 1.0f);
				break;
			}
			GUILayout.Label (dMissing.type);
			GUI.color = defColor;
			GUILayout.EndHorizontal ();
		}
		EditorGUILayout.EndScrollView();
	}

	string FormatSizeString(int memSizeKB)
	{
		if (memSizeKB<1024) return ""+memSizeKB+"k";
		else
		{
			float memSizeMB=((float)memSizeKB)/1024.0f;
			return memSizeMB.ToString("0.00")+"Mb";
		}
	}


	TextureDetails FindTextureDetails(Texture tTexture)
	{
		foreach (TextureDetails tTextureDetails in ActiveTextures)
		{
			if (tTextureDetails.texture==tTexture) return tTextureDetails;
		}
		return null;

	}

	MaterialDetails FindMaterialDetails(Material tMaterial)
	{
		foreach (MaterialDetails tMaterialDetails in ActiveMaterials)
		{
			if (tMaterialDetails.material==tMaterial) return tMaterialDetails;
		}
		return null;

	}

	MeshDetails FindMeshDetails(Mesh tMesh)
	{
		foreach (MeshDetails tMeshDetails in ActiveMeshDetails)
		{
			if (tMeshDetails.mesh==tMesh) return tMeshDetails;
		}
		return null;

	}

	AnimationClipDetails FindAnimationClipDetails(AnimationClip tAnimationClip)
	{
		foreach (AnimationClipDetails tAnimationClipDetails in ActiveAnimationClips)
		{
			if (tAnimationClipDetails.animationClip == tAnimationClip) return tAnimationClipDetails;
		}
		return null;

	}

	AudioClipDetails FindAudioClipDetails(AudioClip tAudioClip)
	{
		foreach (AudioClipDetails tAudioClipDetails in ActiveAudioClips)
		{
			if (tAudioClipDetails.audioClip == tAudioClip) return tAudioClipDetails;
		}
		return null;

	}

	void CheckResources()
	{
		ActiveTextures.Clear();
		ActiveMaterials.Clear();
		ActiveMeshDetails.Clear();
		ActiveAnimationClips.Clear();
		ActiveAudioClips.Clear();
		MissingObjects.Clear ();
  
		thingsMissing = false;

		Renderer[] renderers = FindObjects<Renderer>();

		MaterialDetails skyMat = new MaterialDetails ();

		HashSet<Shader> uniqueShaders = new HashSet<Shader>();

		skyMat.material = RenderSettings.skybox;
		skyMat.isSky = true;
		ActiveMaterials.Add (skyMat);

		//Debug.Log("Total renderers "+renderers.Length);
		foreach (Renderer renderer in renderers)
		{
			//Debug.Log("Renderer is "+renderer.name);
			foreach (Material material in renderer.sharedMaterials)
			{

				MaterialDetails tMaterialDetails = FindMaterialDetails(material);
				if (tMaterialDetails == null)
				{
					tMaterialDetails = new MaterialDetails();
					tMaterialDetails.material = material;
					ActiveMaterials.Add(tMaterialDetails);
					

				}

				if (material != null)
				{
					uniqueShaders.Add(material.shader);
				}
				tMaterialDetails.FoundInRenderers.Add(renderer);
			}

			shaderCount = uniqueShaders.Count;

			if (renderer is SpriteRenderer)
			{
				SpriteRenderer tSpriteRenderer = (SpriteRenderer)renderer;

				if (tSpriteRenderer.sprite != null) {
					var tSpriteTextureDetail = GetTextureDetail (tSpriteRenderer.sprite.texture, renderer);
					if (!ActiveTextures.Contains (tSpriteTextureDetail)) {
						ActiveTextures.Add (tSpriteTextureDetail);
					}
				} else if (tSpriteRenderer.sprite == null) {
					MissingGraphic tMissing = new MissingGraphic ();
					tMissing.Object = tSpriteRenderer.transform;
					tMissing.type = "sprite";
					tMissing.name = tSpriteRenderer.transform.name;
					MissingObjects.Add (tMissing);
					thingsMissing = true;
				}
			}
		}

		if (IncludeGuiElements)
		{
			Graphic[] graphics = FindObjects<Graphic>();

			foreach(Graphic graphic in graphics)
			{
				if (graphic.mainTexture)
				{
					var tSpriteTextureDetail = GetTextureDetail(graphic.mainTexture, graphic);
					if (!ActiveTextures.Contains(tSpriteTextureDetail))
					{
						ActiveTextures.Add(tSpriteTextureDetail);
					}
				}

				if (graphic.materialForRendering)
				{
					MaterialDetails tMaterialDetails = FindMaterialDetails(graphic.materialForRendering);
					if (tMaterialDetails == null)
					{
						tMaterialDetails = new MaterialDetails();
						tMaterialDetails.material = graphic.materialForRendering;
						tMaterialDetails.isgui = true;
						ActiveMaterials.Add(tMaterialDetails);
					}
					tMaterialDetails.FoundInGraphics.Add(graphic);
				}
			}

			Button[] buttons = FindObjects<Button>();
			foreach (Button button in buttons)
			{
				CheckButtonSpriteState(button, button.spriteState.disabledSprite);
				CheckButtonSpriteState(button, button.spriteState.highlightedSprite);
				CheckButtonSpriteState(button, button.spriteState.pressedSprite);
			}
		}

		foreach (MaterialDetails tMaterialDetails in ActiveMaterials)
		{
			Material tMaterial = tMaterialDetails.material;
			if (tMaterial != null)
			{
				var dependencies = EditorUtility.CollectDependencies(new UnityEngine.Object[] { tMaterial });
				foreach (Object obj in dependencies)
				{
					if (obj is Texture)
					{
						Texture tTexture = obj as Texture;
						var tTextureDetail = GetTextureDetail(tTexture, tMaterial, tMaterialDetails);
						tTextureDetail.isSky = tMaterialDetails.isSky;
						tTextureDetail.instance = tMaterialDetails.instance;
						tTextureDetail.isgui = tMaterialDetails.isgui;
						if (!ActiveTextures.Contains (tTextureDetail)) {
							ActiveTextures.Add (tTextureDetail);
						}
					}
				}

				//if the texture was downloaded, it won't be included in the editor dependencies
				if (tMaterial.HasProperty ("_MainTex")) {
					if (tMaterial.mainTexture != null && !dependencies.Contains (tMaterial.mainTexture)) {
						var tTextureDetail = GetTextureDetail (tMaterial.mainTexture, tMaterial, tMaterialDetails);
						if (!ActiveTextures.Contains (tTextureDetail)) {
							ActiveTextures.Add (tTextureDetail);
						}
					}
				}
			}
		}


		MeshFilter[] meshFilters = FindObjects<MeshFilter>();

		foreach (MeshFilter tMeshFilter in meshFilters)
		{
			Mesh tMesh = tMeshFilter.sharedMesh;
			if (tMesh != null)
			{
				MeshDetails tMeshDetails = FindMeshDetails(tMesh);
				if (tMeshDetails == null)
				{
					tMeshDetails = new MeshDetails();
					tMeshDetails.mesh = tMesh;
					ActiveMeshDetails.Add(tMeshDetails);
				}
				tMeshDetails.FoundInMeshFilters.Add(tMeshFilter);
			} else if (tMesh == null && tMeshFilter.transform.GetComponent("TextContainer")== null) {
				MissingGraphic tMissing = new MissingGraphic ();
				tMissing.Object = tMeshFilter.transform;
				tMissing.type = "mesh";
				tMissing.name = tMeshFilter.transform.name;
				MissingObjects.Add (tMissing);
				thingsMissing = true;
			}

			var meshRenderrer = tMeshFilter.transform.GetComponent<MeshRenderer>();
				
			if (meshRenderrer == null || meshRenderrer.sharedMaterial == null) {
				MissingGraphic tMissing = new MissingGraphic ();
				tMissing.Object = tMeshFilter.transform;
				tMissing.type = "material";
				tMissing.name = tMeshFilter.transform.name;
				MissingObjects.Add (tMissing);
				thingsMissing = true;
			}
		}

		SkinnedMeshRenderer[] skinnedMeshRenderers = FindObjects<SkinnedMeshRenderer>();

		foreach (SkinnedMeshRenderer tSkinnedMeshRenderer in skinnedMeshRenderers)
		{
			Mesh tMesh = tSkinnedMeshRenderer.sharedMesh;
			if (tMesh != null)
			{
				MeshDetails tMeshDetails = FindMeshDetails(tMesh);
				if (tMeshDetails == null)
				{
					tMeshDetails = new MeshDetails();
					tMeshDetails.mesh = tMesh;
					ActiveMeshDetails.Add(tMeshDetails);
				}
				tMeshDetails.FoundInSkinnedMeshRenderer.Add(tSkinnedMeshRenderer);
			} else if (tMesh == null) {
				MissingGraphic tMissing = new MissingGraphic ();
				tMissing.Object = tSkinnedMeshRenderer.transform;
				tMissing.type = "mesh";
				tMissing.name = tSkinnedMeshRenderer.transform.name;
				MissingObjects.Add (tMissing);
				thingsMissing = true;
			}
			if (tSkinnedMeshRenderer.sharedMaterial == null) {
				MissingGraphic tMissing = new MissingGraphic ();
				tMissing.Object = tSkinnedMeshRenderer.transform;
				tMissing.type = "material";
				tMissing.name = tSkinnedMeshRenderer.transform.name;
				MissingObjects.Add (tMissing);
				thingsMissing = true;
			}
		}

		LODGroup[] lodGroups = FindObjects<LODGroup>();

		// Check if any LOD groups have no renderers
		foreach (var group in lodGroups)
		{
			var lods = group.GetLODs();
			for (int i = 0, l = lods.Length; i < l; i++)
			{
				if (lods[i].renderers.Length == 0)
				{
					MissingGraphic tMissing = new MissingGraphic();
					tMissing.Object = group.transform;
					tMissing.type = "lod";
					tMissing.name = group.transform.name;
					MissingObjects.Add(tMissing);
					thingsMissing = true;
				}
			}
		}


		if (IncludeSpriteAnimations)
		{
			Animator[] animators = FindObjects<Animator>();
			foreach (Animator anim in animators)
			{
#if UNITY_4_6 || UNITY_4_5 || UNITY_4_4 || UNITY_4_3
				UnityEditorInternal.AnimatorController ac = anim.runtimeAnimatorController as UnityEditorInternal.AnimatorController;
#elif UNITY_5 || UNITY_5_3_OR_NEWER
				UnityEditor.Animations.AnimatorController ac = anim.runtimeAnimatorController as UnityEditor.Animations.AnimatorController;
#endif

				//Skip animators without layers, this can happen if they don't have an animator controller.
				if (!ac || ac.layers == null || ac.layers.Length == 0)
					continue;

				for (int x = 0; x < anim.layerCount; x++)
				{
#if UNITY_4_6 || UNITY_4_5 || UNITY_4_4 || UNITY_4_3
					UnityEditorInternal.StateMachine sm = ac.GetLayer(x).stateMachine;
					int cnt = sm.stateCount;
#elif UNITY_5 || UNITY_5_3_OR_NEWER
					UnityEditor.Animations.AnimatorStateMachine sm = ac.layers[x].stateMachine;
					int cnt = sm.states.Length;
#endif

					for (int i = 0; i < cnt; i++)
					{
#if UNITY_4_6 || UNITY_4_5 || UNITY_4_4 || UNITY_4_3
						UnityEditorInternal.State state = sm.GetState(i);
						Motion m = state.GetMotion();
#elif UNITY_5 || UNITY_5_3_OR_NEWER
						UnityEditor.Animations.AnimatorState state = sm.states[i].state;
						Motion m = state.motion;
#endif
						if (m != null)
						{
							AnimationClip clip = m as AnimationClip;

							if (clip != null)
							{
								EditorCurveBinding[] ecbs = AnimationUtility.GetObjectReferenceCurveBindings(clip);

								foreach (EditorCurveBinding ecb in ecbs)
								{
									if (ecb.propertyName == "m_Sprite")
									{
										foreach (ObjectReferenceKeyframe keyframe in AnimationUtility.GetObjectReferenceCurve(clip, ecb))
										{
											Sprite tSprite = keyframe.value as Sprite;

											if (tSprite != null)
											{
												var tTextureDetail = GetTextureDetail(tSprite.texture, anim);
												if (!ActiveTextures.Contains(tTextureDetail))
												{
													ActiveTextures.Add(tTextureDetail);
												}
											}
										}
									}
								}
							}
						}
					}
				}

			}
		}

		if (IncludeScriptReferences)
		{
			MonoBehaviour[] scripts = FindObjects<MonoBehaviour>();
			foreach (MonoBehaviour script in scripts)
			{
				BindingFlags flags = BindingFlags.Public | BindingFlags.Instance; // only public non-static fields are bound to by Unity.
				FieldInfo[] fields = script.GetType().GetFields(flags);

				foreach (FieldInfo field in fields)
				{
					System.Type fieldType = field.FieldType;
					if (fieldType == typeof(Sprite))
					{
						Sprite tSprite = field.GetValue(script) as Sprite;
						if (tSprite != null)
						{
							var tSpriteTextureDetail = GetTextureDetail(tSprite.texture, script);
							if (!ActiveTextures.Contains(tSpriteTextureDetail))
							{
								ActiveTextures.Add(tSpriteTextureDetail);
							}
						}
					}if (fieldType == typeof(Mesh))
					{
						Mesh tMesh = field.GetValue(script) as Mesh;
						if (tMesh != null)
						{
							MeshDetails tMeshDetails = FindMeshDetails(tMesh);
							if (tMeshDetails == null)
							{
								tMeshDetails = new MeshDetails();
								tMeshDetails.mesh = tMesh;
								tMeshDetails.instance = true;
								ActiveMeshDetails.Add(tMeshDetails);
							}
						}
					}if (fieldType == typeof(Material))
					{
						Material tMaterial = field.GetValue(script) as Material;
						if (tMaterial != null)
						{
							MaterialDetails tMatDetails = FindMaterialDetails(tMaterial);
							if (tMatDetails == null)
							{
								tMatDetails = new MaterialDetails();
								tMatDetails.instance = true;
								tMatDetails.material = tMaterial;
								if(!ActiveMaterials.Contains(tMatDetails))
									ActiveMaterials.Add(tMatDetails);
							}
							if (tMaterial.HasProperty ("_MainTex"))
							{
								if (tMaterial.mainTexture)
								{
									var tSpriteTextureDetail = GetTextureDetail(tMaterial.mainTexture);
									if (!ActiveTextures.Contains(tSpriteTextureDetail))
									{
										ActiveTextures.Add(tSpriteTextureDetail);
									}
								}
							}
							var dependencies = EditorUtility.CollectDependencies(new UnityEngine.Object[] { tMaterial });
							foreach (Object obj in dependencies)
							{
								if (obj is Texture)
								{
									Texture tTexture = obj as Texture;
									var tTextureDetail = GetTextureDetail(tTexture, tMaterial, tMatDetails);
									if(!ActiveTextures.Contains(tTextureDetail))
										ActiveTextures.Add(tTextureDetail);
								}
							}
						}
					}
				}
			}
		}

		{
			MonoBehaviour[] scripts = FindObjects<MonoBehaviour>();
			foreach (MonoBehaviour script in scripts)
			{
				BindingFlags flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance; // only public non-static fields are bound to by Unity.
				FieldInfo[] fields = script.GetType().GetFields(flags);

				foreach (FieldInfo field in fields)
				{
					System.Type fieldType = field.FieldType;
					if (fieldType == typeof(AnimationClip))
					{
						AnimationClip tAnimationClip = field.GetValue(script) as AnimationClip;
						if (tAnimationClip != null)
						{
							AnimationClipDetails tAnimationClipDetails = FindAnimationClipDetails(tAnimationClip);
							if (tAnimationClipDetails == null)
							{
								tAnimationClipDetails = new AnimationClipDetails();
								tAnimationClipDetails.animationClip = tAnimationClip;
								tAnimationClipDetails.instance = true;
								tAnimationClipDetails.FoundInMonoBehaviour.Add(script);
								ActiveAnimationClips.Add(tAnimationClipDetails);
							}
						}
					}
				}
			}

#if RESOURCECHECKER_TIMELINE
			PlayableDirector[] playableDirectors = FindObjects<PlayableDirector>();
			foreach (PlayableDirector playableDirector in playableDirectors)
			{
				TimelineAsset tTimelineAsset = playableDirector.playableAsset as TimelineAsset;
				if (tTimelineAsset != null)
				{
					foreach (TrackAsset tTimelineTrack in tTimelineAsset.GetRootTracks())
					{
						ParseAnimationTrack(tTimelineTrack);

						foreach (TrackAsset tChildTimelineTrack in tTimelineTrack.GetChildTracks())
						{
							ParseAnimationTrack(tChildTimelineTrack);
						}
					}
				}
			}
#endif
		}

		{
			MonoBehaviour[] scripts = FindObjects<MonoBehaviour>();
			foreach (MonoBehaviour script in scripts)
			{
				BindingFlags flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance; // only public non-static fields are bound to by Unity.
				FieldInfo[] fields = script.GetType().GetFields(flags);

				foreach (FieldInfo field in fields)
				{
					System.Type fieldType = field.FieldType;
					if (fieldType == typeof(AudioClip))
					{
						AudioClip tAudioClip = field.GetValue(script) as AudioClip;
						if (tAudioClip != null)
						{
							AudioClipDetails tAudioClipDetails = FindAudioClipDetails(tAudioClip);
							if (tAudioClipDetails == null)
							{
								tAudioClipDetails = new AudioClipDetails();
								tAudioClipDetails.audioClip = tAudioClip;
								tAudioClipDetails.instance = true;
								tAudioClipDetails.FoundInMonoBehaviour.Add(script);
								ActiveAudioClips.Add(tAudioClipDetails);
							}
						}
					}
				}
			}

#if RESOURCECHECKER_TIMELINE
			PlayableDirector[] playableDirectors = FindObjects<PlayableDirector>();
			foreach (PlayableDirector playableDirector in playableDirectors)
			{
				TimelineAsset tTimelineAsset = playableDirector.playableAsset as TimelineAsset;
				if (tTimelineAsset != null)
				{
					foreach (TrackAsset tTimelineTrack in tTimelineAsset.GetRootTracks())
					{
						ParseAudioTrack(tTimelineTrack);
						foreach (TrackAsset tChildTimelineTrack in tTimelineTrack.GetChildTracks())
						{
							ParseAudioTrack(tChildTimelineTrack);
						}
					}
				}
			}
#endif
		}

		TotalTextureMemory = 0;
		foreach (TextureDetails tTextureDetails in ActiveTextures) TotalTextureMemory += tTextureDetails.memSizeKB;

		TotalMeshVertices = 0;
		foreach (MeshDetails tMeshDetails in ActiveMeshDetails) TotalMeshVertices += tMeshDetails.mesh.vertexCount;

		// Sort by size, descending

		ActiveMeshDetails.Sort(delegate(MeshDetails details1, MeshDetails details2) { return details2.mesh.vertexCount - details1.mesh.vertexCount; });

		// Sort shader by name descending

		TotalAnimationClipMemory = 0;
		foreach (AnimationClipDetails tAnimationClipDetails in ActiveAnimationClips) TotalAnimationClipMemory += CalcurateAnimationClipSizeKb(tAnimationClipDetails.animationClip);


		collectedInPlayingMode = Application.isPlaying;
	}

	public class ShaderNameComparer : IComparer<MaterialDetails>
	{
		public int Compare(MaterialDetails x, MaterialDetails y)
		{
			// Compare the names of the objects
			return string.Compare(x.shaderName, y.shaderName, StringComparison.OrdinalIgnoreCase);
		}
	}

	public class ShaderBrandNameComparer : IComparer<MaterialDetails>
	{
		public int Compare(MaterialDetails x, MaterialDetails y)
		{
			// Compare the names of the objects
			return string.Compare(x.shaderBrand, y.shaderBrand, StringComparison.OrdinalIgnoreCase);
		}
	}
	public class ObjectNameComparer : IComparer<MaterialDetails>
	{
		public int Compare(MaterialDetails x, MaterialDetails y)
		{
			// Compare the names of the objects
			return string.Compare(x.material.name, y.material.name, StringComparison.OrdinalIgnoreCase);
		}
	}

	public class TextureNameComparer : IComparer<TextureDetails>
	{
		public int Compare(TextureDetails x, TextureDetails y)
		{
			// Compare the names of the objects
			return string.Compare(x.texture.name, y.texture.name, StringComparison.OrdinalIgnoreCase);
		}
	}
	public class TextureFormatNameComparer : IComparer<TextureDetails>
	{
		public int Compare(TextureDetails x, TextureDetails y)
		{
			string textureAssetPathX = AssetDatabase.GetAssetPath(x.texture);
			string textureAssetPathY = AssetDatabase.GetAssetPath(y.texture);
			string fileformatX = Path.GetExtension(textureAssetPathX);
			string fileformatY = Path.GetExtension(textureAssetPathY);
			// Compare the names of the objects
			return string.Compare(fileformatX, fileformatY, StringComparison.OrdinalIgnoreCase);
		}
	}


	void SortTextureName()
	{
		ActiveTextures.Sort(new TextureNameComparer());
		ActiveTextures = ActiveTextures.Distinct().ToList();
	}

	void SortTextureSize()
	{
		ActiveTextures.Sort(delegate (TextureDetails details1, TextureDetails details2) { return details2.memSizeKB - details1.memSizeKB; });
		ActiveTextures = ActiveTextures.Distinct().ToList();
	}

	void SortTextureFormat()
	{
		ActiveTextures.Sort(new TextureFormatNameComparer());
		ActiveTextures = ActiveTextures.Distinct().ToList();
	}

	void SortTextureAlpha()
	{
		ActiveTextures.Sort((a, b) => b.hasAlpha.CompareTo(a.hasAlpha));
		ActiveTextures = ActiveTextures.Distinct().ToList();
	}
	void SortMaterialShader()
	{
		ActiveMaterials.Sort(new ShaderNameComparer());
		ActiveMaterials = ActiveMaterials.Distinct().ToList();
	}
	void SortMaterialShaderBrand()
	{
		ActiveMaterials.Sort(new ShaderBrandNameComparer());
		ActiveMaterials = ActiveMaterials.Distinct().ToList();
	}

	void SortMaterialName()
	{
		ActiveMaterials.Sort(new ObjectNameComparer());
		ActiveMaterials = ActiveMaterials.Distinct().ToList();
	}

	private void CheckButtonSpriteState(Button button, Sprite sprite) 
	{
		if (sprite == null) return;
		
		var texture = sprite.texture;
		var tButtonTextureDetail = GetTextureDetail(texture, button);
		if (!ActiveTextures.Contains(tButtonTextureDetail))
		{
			ActiveTextures.Add(tButtonTextureDetail);
		}
	}
	
	private static GameObject[] GetAllRootGameObjects()
	{
#if !UNITY_5 && !UNITY_5_3_OR_NEWER
		return UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects().ToArray();
#else
		List<GameObject> allGo = new List<GameObject>();
		for (int sceneIdx = 0; sceneIdx < UnityEngine.SceneManagement.SceneManager.sceneCount; ++sceneIdx){
			//only add the scene to the list if it's currently loaded.
			if (UnityEngine.SceneManagement.SceneManager.GetSceneAt(sceneIdx).isLoaded) {
				allGo.AddRange(UnityEngine.SceneManagement.SceneManager.GetSceneAt(sceneIdx).GetRootGameObjects().ToArray());
			}
		}
		return allGo.ToArray();
#endif
	}

	private T[] FindObjects<T>() where T : Object
	{
		if (IncludeDisabledObjects) {
			List<T> meshfilters = new List<T> ();
			GameObject[] allGo = GetAllRootGameObjects();
			foreach (GameObject go in allGo) {
				Transform[] tgo = go.GetComponentsInChildren<Transform> (true).ToArray ();
				foreach (Transform tr in tgo) {
					if (tr.GetComponent<T> ())
						meshfilters.Add (tr.GetComponent<T> ());
				}
			}
			return (T[])meshfilters.ToArray ();
		}
		else
#if UNITY_2022_1_OR_NEWER
			return (T[])FindObjectsByType(typeof(T), FindObjectsSortMode.None);
#else
			return (T[])FindObjectsOfType(typeof(T));
#endif
	}

	private TextureDetails GetTextureDetail(Texture tTexture, Material tMaterial, MaterialDetails tMaterialDetails)
	{
		TextureDetails tTextureDetails = GetTextureDetail(tTexture);

		tTextureDetails.FoundInMaterials.Add(tMaterial);
		foreach (Renderer renderer in tMaterialDetails.FoundInRenderers)
		{
			if (!tTextureDetails.FoundInRenderers.Contains(renderer)) tTextureDetails.FoundInRenderers.Add(renderer);
		}
		return tTextureDetails;
	}





	private TextureDetails GetTextureDetail(Texture tTexture, Renderer renderer)
	{
		TextureDetails tTextureDetails = GetTextureDetail(tTexture);

		tTextureDetails.FoundInRenderers.Add(renderer);
		return tTextureDetails;
	}

	private TextureDetails GetTextureDetail(Texture tTexture, Animator animator)
	{
		TextureDetails tTextureDetails = GetTextureDetail(tTexture);

		tTextureDetails.FoundInAnimators.Add(animator);
		return tTextureDetails;
	}

	private TextureDetails GetTextureDetail(Texture tTexture, Graphic graphic)
	{
		TextureDetails tTextureDetails = GetTextureDetail(tTexture);

		tTextureDetails.FoundInGraphics.Add(graphic);
		return tTextureDetails;
	}

	private TextureDetails GetTextureDetail(Texture tTexture, MonoBehaviour script)
	{
		TextureDetails tTextureDetails = GetTextureDetail(tTexture);

		tTextureDetails.FoundInScripts.Add(script);
		return tTextureDetails;
	}

	private TextureDetails GetTextureDetail(Texture tTexture, Button button) 
	{
		TextureDetails tTextureDetails = GetTextureDetail(tTexture);

		if (!tTextureDetails.FoundInButtons.Contains(button))
		{
			tTextureDetails.FoundInButtons.Add(button);
		}
		return tTextureDetails;
	}

	private TextureDetails GetTextureDetail(Texture tTexture)
	{
		TextureDetails tTextureDetails = FindTextureDetails(tTexture);
		if (tTextureDetails == null)
		{
			tTextureDetails = new TextureDetails();
			tTextureDetails.texture = tTexture;
			tTextureDetails.isCubeMap = tTexture is Cubemap;

			int memSize = CalculateTextureSizeBytes(tTexture);

			TextureFormat tFormat = TextureFormat.RGBA32;
			int tMipMapCount = 1;
			if (tTexture is Texture2D)
			{
				tFormat = (tTexture as Texture2D).format;
				tMipMapCount = (tTexture as Texture2D).mipmapCount;
			}
			if (tTexture is Cubemap)
			{
				tFormat = (tTexture as Cubemap).format;
				memSize = 8 * tTexture.height * tTexture.width;
			}
			if(tTexture is Texture2DArray){
				tFormat = (tTexture as Texture2DArray).format;
				tMipMapCount = 10;
			}

			tTextureDetails.memSizeKB = memSize / 1024;
			tTextureDetails.format = tFormat;
			tTextureDetails.mipMapCount = tMipMapCount;

		}

		return tTextureDetails;
	}

	static void PlayerPrefs_SetInspectType(string str, ref InspectType field, InspectType value)
	{
		if (field != value)
		{
			field = value;
			PlayerPrefs.SetInt(str, (int)field);
		}
	}

	static void PlayerPrefs_SetBool(string str, ref bool field, bool value)
	{
		if (field != value)
		{
			field = value;
			PlayerPrefs.SetInt(str, field ? 1 : 0);
		}
	}

	static bool PlayerPrefs_GetBool(string str, bool defaultValue)
	{
		return PlayerPrefs.GetInt(str, defaultValue ? 1 : 0) != 0;
	}

	class AssetSizeCache
	{
		public void Refresh()
		{
			AssetDatabase.Refresh();
			_dict.Clear();
		}

		public int GetSize(UnityEngine.Object obj)
		{
			var path = AssetDatabase.GetAssetPath(obj);
			if (string.IsNullOrEmpty(path))
				return 0;
			return GetSize(path);
		}

		public int GetSize(string path)
		{
			if (_dict.ContainsKey(path))
				return _dict[path];

			int size = 0;
			FileInfo fileInfo = new FileInfo(path);
			if (fileInfo != null)
			{
				size = (int)fileInfo.Length;
			}
			_dict.Add(path, size);
			return size;
		}
		Dictionary<string, int> _dict = new();
	}

	int CalcurateAnimationClipSizeKb(AnimationClip animationClip)
	{
		if (animationClip != null)
		{
			return _assetSizeCache.GetSize(animationClip) / 1024;
		}
		return 0;
	}

	void PageButtons(ref int pageIndex, int pageCount)
	{
		if (pageCount <= 1)
		{
			pageIndex = 0;
			return;
		}
		pageIndex = Mathf.Clamp(pageIndex, 0, pageCount - 1);
		GUILayout.Label($"Page:{pageIndex + 1}", GUILayout.Width(150));
		EditorGUILayout.BeginHorizontal();
		for (int i = 0; i < pageCount; i++)
		{
			GUI.color = (i == pageIndex) ? new Color(1.0f, 1.0f, 1.0f, 1.0f) : new Color(0.8f, 0.8f, 0.8f, 1.0f);
			if (GUILayout.Button($"Page.{i + 1}", GUILayout.Width(50)))
			{
				pageIndex = i;
			}
		}
		EditorGUILayout.EndHorizontal();
		GUI.color = new Color(1.0f, 1.0f, 1.0f, 1.0f);
	}

	void ParseAnimationTrack(TrackAsset timelineTrack)
	{
		var trackType = timelineTrack.GetType();
		if (trackType.Name == "AnimationTrack")
		{
			var animationTrack = timelineTrack as AnimationTrack;
			foreach (TimelineClip timelineClip in animationTrack.GetClips())
			{
				var playableAsset = timelineClip.asset as AnimationPlayableAsset;
				if (playableAsset != null)
				{
					AnimationClip animationClip = playableAsset.clip;
					if (animationClip != null)
					{
						AnimationClipDetails details = FindAnimationClipDetails(animationClip);
						if (details == null)
						{
							details = new AnimationClipDetails();
							details.animationClip = animationClip;
							details.FoundInPlayableAsset.Add(playableAsset);
							ActiveAnimationClips.Add(details);
						}
					}
				}
			}
		}
	}

	void ParseAudioTrack(TrackAsset timelineTrack)
	{
		var trackType = timelineTrack.GetType();
		if (trackType.Name == "AudioTrack")
		{
			var audioTrack = timelineTrack as AudioTrack;
			foreach (TimelineClip timelineClip in audioTrack.GetClips())
			{
				var audioPlayableAsset = timelineClip.asset as AudioPlayableAsset;
				if (audioPlayableAsset != null)
				{
					AudioClip audioClip = audioPlayableAsset.clip;
					if (audioClip != null)
					{
						AudioClipDetails details = FindAudioClipDetails(audioClip);
						if (details == null)
						{
							details = new AudioClipDetails();
							details.audioClip = audioClip;
							details.FoundInPlayableAsset.Add(audioPlayableAsset);
							ActiveAudioClips.Add(details);
						}
					}
				}
			}
		}
		//if (trackType.Name == "OnetimeAudioTrack")
		//{
		//	var audioTrack = timelineTrack as OnetimeAudioTrack;
		//	foreach (TimelineClip timelineClip in audioTrack.GetClips())
		//	{
		//		var audioClip = timelineClip.asset as OnetimeAudioClip;
		//		if (audioClip != null)
		//		{
		//			if (audioClip.audioClip != null)
		//			{
		//				AudioClipDetails details = FindAudioClipDetails(audioClip.audioClip);
		//				if (details == null)
		//				{
		//					details = new AudioClipDetails();
		//					details.audioClip = audioClip.audioClip;
		//					details.FoundInPlayableAsset.Add(audioClip);
		//					ActiveAudioClips.Add(details);
		//				}
		//			}
		//		}
		//	}
		//}
	}
	
	void FilterTextField(Action<string> onProcessFilter)
	{
		EditorGUILayout.Space();
		GUILayout.BeginHorizontal();
		_filterText = EditorGUILayout.TextField("Name Filter", _filterText, GUILayout.Width(300));
		if (!string.IsNullOrEmpty(_filterText))
		{
			var tmp = _filterText;
			if (!tmp.Contains('*') && !tmp.Contains('?'))
			{
				tmp = $"*{tmp}*";
			}
			onProcessFilter.Invoke(tmp);
		}

		_ignoreCase = EditorGUILayout.ToggleLeft("Ignore Case", _ignoreCase, GUILayout.Width(150));

		GUILayout.EndHorizontal();
		EditorGUILayout.Space();
	}

	/// <summary>
	/// ワイルドカードでパターン一致するか
	public static bool Wildcard(string text, string pattern, bool ignoreCase = true)
	{
		char[] textArray = text.ToCharArray();
		char[] patternArray = pattern.ToCharArray();
		int textLength = text.Length;
		int patternLength = pattern.Length;

		// empty pattern can only
		// match with empty string.
		// Base Case :
		if (patternLength == 0)
			return (textLength == 0);

		// step-1 :
		// initialize markers :
		int i = 0, j = 0, textIndex = -1, patternIndex = -1;

		while (i < textLength)
		{
			// For step - (2, 5)
			if (j < patternLength && (ignoreCase ? (string.Compare(textArray[i].ToString(), patternArray[j].ToString(), StringComparison.OrdinalIgnoreCase) == 0) : (textArray[i] == patternArray[j])))
			{
				i++;
				j++;
			}

			// For step - (3)
			else if (j < patternLength && patternArray[j] == '?')
			{
				i++;
				j++;
			}

			// For step - (4)
			else if (j < patternLength && patternArray[j] == '*')
			{
				textIndex = i;
				patternIndex = j;
				j++;
			}

			// For step - (5)
			else if (patternIndex != -1)
			{
				j = patternIndex + 1;
				i = textIndex + 1;
				textIndex++;
			}

			// For step - (6)
			else
			{
				return false;
			}
		}

		// For step - (7)
		while (j < patternLength && patternArray[j] == '*')
		{
			j++;
		}

		// Final Check
		if (j == patternLength)
		{
			return true;
		}

		return false;
	}
}
