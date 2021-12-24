using UnityEngine.SceneManagement;
using System.Collections.Generic;
using System.Collections;
using UnityEngine;
using UnityEditor;
using System.IO;
using System;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

#if UNITY_EDITOR

// Automatically convert any texture file in EditorCaptures to Non Power of Two
class EditorCapturePreProcessor : AssetPostprocessor
{
    void OnPreprocessTexture()
    {
        if (assetPath.Contains("EditorCaptures"))
        {
            TextureImporter textureImporter  = (TextureImporter)assetImporter;
            textureImporter.npotScale = TextureImporterNPOTScale.None;
        }
    }
}

[System.Serializable]
public class EditorCapture : EditorWindow
{
    string editorCapturesFolderPath = "EditorCaptures";
    string path = "";
    string projectRelatedPath = "";

    float displayRatio = .5f;				//Default display ratio
	bool showText = true;					//By default show textSize
	int textSize = 26;						//Default capture name text size
	Color textColor = Color.white;			//Default capture name text color
	float mouseWheelMultiplier = 0.01f;		//

	Vector2 textureOffset = Vector2.zero;
	Vector2 oldMousePosition = Vector2.zero;
	Vector2 textureScrollPosition = Vector2.zero;
	Vector2 initialTextureScrollPosition = Vector2.zero;
	Vector2 mouseDragLastPosition = Vector2.zero;
	
	Vector2 scrollPosition = Vector2.zero;
	
	Vector2 startDrag = Vector2.zero;
	

	string captureName = "Untitled Capture";
	
	float margin = 10;
	float left = 200;
	float headerHeight = 140;
	
	
	string lastCapturedImageName = "";
	
	List<string> selectedFilename = new List<string>();
	List<Texture2D> selectedTextures = new List<Texture2D>();
	int textureIndex = 0;
	GUIStyle labelStyle = new GUIStyle();
	GUIStyle captureListButtonStyle;

	void Awake()
	{
		path = Application.dataPath+"/"+editorCapturesFolderPath+"/";
		projectRelatedPath = "Assets/"+editorCapturesFolderPath+"/";
		labelStyle.fontSize = textSize;
		labelStyle.normal.textColor = textColor;

	}
	
	void Start()
	{
				
	}
	
    // Add menu named "Editor Capture" to the Window menu
    [MenuItem("Window/Editor Capture")]
    static void Init()
    {
        // Get existing open window or if none, make a new one:
        // EditorCapture window = (EditorCapture)EditorWindow.GetWindowWithRect(typeof(EditorCapture), new Rect(0, 0, 1280f/EditorGUIUtility.pixelsPerPoint, 720f/EditorGUIUtility.pixelsPerPoint));
        EditorCapture window = (EditorCapture)EditorWindow.GetWindow(typeof(EditorCapture));
		window.minSize = new Vector2(1280f/EditorGUIUtility.pixelsPerPoint, 720f/EditorGUIUtility.pixelsPerPoint);
        window.Show();
		
    }

	void OnGUI()
    {
		// Debug.Log(Screen.width+" "+Screen.height);
		Event e = Event.current;
		manageEvents(Event.current);
		
		
		bool folderExists = Directory.Exists(path);
		// Debug.Log(path+" "+folderExists);
		
		GUILayout.BeginArea(new Rect(left + 2*margin,margin, Screen.width/EditorGUIUtility.pixelsPerPoint - (left+3*margin),headerHeight));
			displayRatio = EditorGUILayout.Slider("Ratio", displayRatio, 0, 5, GUILayout.Width(410));
			showText = EditorGUILayout.Toggle("Show Capture Name", showText);
			textSize = EditorGUILayout.IntField("Capture Name Size", Mathf.Clamp(textSize,0,200), GUILayout.Width(300));
			textColor = EditorGUILayout.ColorField("Capture Name Color", textColor, GUILayout.Width(300));
		GUILayout.EndArea();
		
        GUILayout.BeginArea(new Rect(margin,margin,left,Screen.height/EditorGUIUtility.pixelsPerPoint-2*margin));
			GUILayout.BeginVertical();
				captureName = EditorGUILayout.TextField(captureName, GUILayout.ExpandWidth(true));
				
				GUI.enabled = captureName != "";
				if (GUILayout.Button("Capture Game View", GUILayout.Height(50)))
				{
					DateTime now = DateTime.Now;
					
					lastCapturedImageName = SceneManager.GetActiveScene().name+"_"+
											now.Year+now.Month.ToString().PadLeft(2,'0')+
											now.Day.ToString().PadLeft(2,'0')+"_"+
											now.Hour.ToString().PadLeft(2,'0')+
											now.Minute.ToString().PadLeft(2,'0')+
											now.Millisecond.ToString().PadLeft(2,'0')+"_"+
											captureName+
											".png"; 
											
				
					// bool exists = System.IO.Directory.Exists(Application.dataPath);
					
					// Debug.Log(folderExists);
					if(!folderExists)
					{
						System.IO.Directory.CreateDirectory(path);
						/*DirectoryInfo info = new DirectoryInfo(path);
						DirectorySecurity security = info.GetAccessControl();
						
						security.AddAccessRule(new FileSystemAccessRule(logonName, FileSystemRight.Modify, InheritanceFlags.ContainerInherit, PropagationFlags.None, AccessControlType.Allow));
						security.AddAccessRule(new FileSystemAccessRule(logonName, FileSystemRight.Modify, InheritanceFlags.ObjectInherit, PropagationFlags.None, AccessControlType.Allow));
						
						info.SetAccessControl(security);*/
						
					}   				
											
					Debug.Log("Captured "+path+lastCapturedImageName);
					ScreenCapture.CaptureScreenshot(path+lastCapturedImageName);
					
					AssetDatabase.Refresh();
				}
				
				
				GUILayout.Label("---------- Capture list ----------");
				GUI.enabled = true;
				

				if(folderExists)
				{
					var info = new DirectoryInfo(path);
					var fileInfo = info.GetFiles();
					
					float buttonHeight = 20;
					float buttonHeightOffset = 2;
					float captureButtonHeight = 50;
					float labelHeight = 30;
					int fileCount = 0;
					foreach(FileInfo file in fileInfo)
							if(!file.Name.EndsWith(".meta"))
								fileCount++;
					
					//Could be done in the Awake but doesn't work for some reasons. 
					if(fileCount > 0)
					{
						captureListButtonStyle = new GUIStyle(GUI.skin.button);
						captureListButtonStyle.alignment = TextAnchor.MiddleLeft;
					}
					
					// GUILayout.BeginArea(new Rect(margin,headerHeight, left, Screen.height - headerHeight));
					// EditorGUILayout.BeginVertical();
					// GUILayout.FlexibleSpace();
						foreach(FileInfo file in fileInfo)
						{
							if(!file.Name.EndsWith(".meta"))
							{
								string captureSuffix = getCaptureName(file.Name);
								bool isSelected = selectedFilename.Contains(file.Name);
								bool firstAdd = false;
								
								Rect position = new Rect(0,margin+captureButtonHeight+labelHeight, left, Screen.height/EditorGUIUtility.pixelsPerPoint - headerHeight + 2*margin);
								Rect view = new Rect(0,margin+captureButtonHeight+labelHeight, left - 2*margin, fileCount*buttonHeight + (fileCount-1)*buttonHeightOffset);
								
								scrollPosition = GUI.BeginScrollView(position, scrollPosition, view);
								
								GUI.color = isSelected ? Color.green : Color.gray;
								if (GUILayout.Button(captureSuffix, captureListButtonStyle, /*GUILayout.Width(left-margin), */GUILayout.Height(buttonHeight)))
								{
									if (isSelected)
									{
										selectedFilename.Remove(file.Name);
									}
									else
									{
										firstAdd = (selectedFilename.Count == 0); 
										selectedFilename.Add(file.Name);
									}
									
									buildTextureList();
									if(firstAdd)
										fitToWindow();
								}
								GUI.color = Color.white;
								
								GUI.EndScrollView();
							}
						}

				}
			
			EditorGUILayout.EndVertical();
		GUILayout.EndArea();

		GUILayout.BeginArea(new Rect(left + 2*margin,(headerHeight - 40),Screen.width/EditorGUIUtility.pixelsPerPoint - (left+3*margin),headerHeight));
		EditorGUILayout.BeginHorizontal();
		GUI.enabled = selectedFilename.Count > 1;
		if (GUILayout.Button(" < ", GUILayout.Width(50), GUILayout.Height(30)))
        {
			decrementTextureIndex();
        }
		
		if (GUILayout.Button(" > ", GUILayout.Width(50), GUILayout.Height(30)))
        {
			incrementTextureIndex();
        }
		
		GUI.enabled = selectedFilename.Count > 0;
		if (GUILayout.Button("Clear", GUILayout.Width(100), GUILayout.Height(30)))
        {
            clearTextures();
        }
		
		
		GUI.enabled = selectedTextures.Count > 0;
		if (GUILayout.Button("Fit to window", GUILayout.Width(100), GUILayout.Height(30)))
        {
			fitToWindow();
		}
		
		GUI.enabled = selectedTextures.Count > 0;
		if (GUILayout.Button("Delete selected", GUILayout.Width(100), GUILayout.Height(30)))
        {
			deleteSelectedImages();
		}
		
		EditorGUILayout.EndHorizontal();
		GUILayout.EndArea();
		
		if(selectedTextures.Count > 0)
		{
			if(textureIndex >= selectedTextures.Count) textureIndex = selectedTextures.Count-1;

			labelStyle.fontSize = textSize;
			labelStyle.normal.textColor = textColor;
			
			float widthLeft = Screen.width/EditorGUIUtility.pixelsPerPoint - (left + 2 * margin);
			float heightLeft = Screen.height/EditorGUIUtility.pixelsPerPoint - (headerHeight + 2 * margin);
			Rect position = new Rect(left + 2*margin, headerHeight, widthLeft, heightLeft);
			Rect view = new Rect(left + 2*margin, headerHeight,  displayRatio*getSelectedTextureSize().x, displayRatio*getSelectedTextureSize().y);
			
			textureScrollPosition = GUI.BeginScrollView(position, textureScrollPosition, view);
							
				GUI.DrawTexture(new Rect(	left + 2*margin + textureOffset.x, 
											headerHeight + textureOffset.y, 
											displayRatio*getSelectedTextureSize().x,
											displayRatio*getSelectedTextureSize().y
										), 
											selectedTextures[textureIndex]);
											
				GUI.color = Color.white;
				if(showText)
					GUI.Label(new Rect(		textureScrollPosition.x + left + 2.5f*margin, 
											textureScrollPosition.y + headerHeight, 
											displayRatio*getSelectedTextureSize().x, 
											displayRatio*getSelectedTextureSize().y
										), 
											getCaptureName(selectedFilename[textureIndex]), 
											labelStyle);
		
			GUI.EndScrollView();
		}

    }
	
	private void manageEvents(Event e)
	{
		switch(e.type)
		{
			case EventType.ScrollWheel:
				if(IsMouseOverImage(e.mousePosition))
				{
					float newDisplayRatio = displayRatio - e.delta.y*mouseWheelMultiplier;
			
					float widthLeft = Mathf.Min(Screen.width/EditorGUIUtility.pixelsPerPoint - (left + 2 * margin), selectedTextures[textureIndex].width * displayRatio);
					float heightLeft = Mathf.Min(Screen.height/EditorGUIUtility.pixelsPerPoint - (headerHeight + 2 * margin), selectedTextures[textureIndex].height * displayRatio);
					
					Vector2 relativeMousePositionToImage = new Vector2((e.mousePosition.x - (left + 2 * margin))/widthLeft, (e.mousePosition.y - (headerHeight))/heightLeft);

					Vector2 overflow = new Vector2(
												getSelectedTextureSize().x * newDisplayRatio - 
												getSelectedTextureSize().x * displayRatio, 
												getSelectedTextureSize().y * newDisplayRatio - 
												getSelectedTextureSize().y * displayRatio); 

					
					setDisplayRatio(newDisplayRatio);
					textureScrollPosition += new Vector2(	overflow.x * relativeMousePositionToImage.x,
															overflow.y * relativeMousePositionToImage.y);
															
					Repaint();
					e.Use();	//To mark it as used, so that the event doesn't trigger anything else
				}
				break;
			
			case EventType.MouseDown:
				if(e.button == 2)
					mouseDragLastPosition = Event.current.mousePosition;
				break;
			case EventType.MouseDrag:
				if(e.button == 2)
				{
					textureScrollPosition += -(e.mousePosition - mouseDragLastPosition);
					mouseDragLastPosition = e.mousePosition;
					Repaint();
				}
				break;
			case EventType.KeyUp:
				if(e.isKey)
					switch(e.keyCode)
					{
						case KeyCode.RightArrow:
						case KeyCode.K:
							incrementTextureIndex();
							Repaint();
							break;
						case KeyCode.LeftArrow:
						case KeyCode.J:
							decrementTextureIndex();
							Repaint();
							break;
						// case KeyCode.Keypad0:
							// Maybe implement selection of capture index by numerical keyboard.
							// break;
					}
				break;
			default: 
				break;
		}
	}
	
	private bool IsMouseOverImage(Vector2 mousePosition){
		if(selectedFilename.Count <= 0) return false;
		
		Rect r = new Rect(left + 2*margin + textureOffset.x, 
											headerHeight + textureOffset.y, 
											displayRatio*getSelectedTextureSize().x,
											displayRatio*getSelectedTextureSize().y);
											
		return r.Contains(mousePosition);
		
	}
	
	private Vector2 getSelectedTextureSize()
	{
		if(textureIndex >= 0 && textureIndex < selectedTextures.Count)
		{
			return new Vector2(selectedTextures[textureIndex].width, selectedTextures[textureIndex].height);
		}
		else
		{
			return Vector2.zero;
		}
		
	}
	
	private void incrementTextureIndex()
	{
		if(textureIndex < selectedFilename.Count-1)
			textureIndex++;
		else 
			textureIndex = 0;
	}
	
	private void decrementTextureIndex()
	{
		if(textureIndex > 0)
			textureIndex--;
		else 
			textureIndex = selectedFilename.Count-1;
	}
	
	private void deleteSelectedImages()
	{
		foreach(string s in selectedFilename)
		{
			File.Delete(path + s);
			File.Delete(path + s+".meta");
			
			// This doesn't work , don't know why. 
			// Debug.Log("Assets/" + s);
			// AssetDatabase.DeleteAsset("Assets/" + s);
		}
		
		clearTextures();
	}
	
	private void clearTextures(){
		selectedFilename.Clear();
        selectedTextures.Clear();
	}

	private void fitToWindow()
	{
		textureOffset = Vector2.zero;
		float widthLeft = Screen.width/EditorGUIUtility.pixelsPerPoint - (left + 3 * margin);
		float heightLeft = Screen.height/EditorGUIUtility.pixelsPerPoint - (headerHeight + 3 * margin);
		float displayRatioW = widthLeft / getSelectedTextureSize().x;
		float displayRatioH = heightLeft / getSelectedTextureSize().y;

		setDisplayRatio(displayRatioW);
		if ((displayRatioW *  getSelectedTextureSize().y) > heightLeft) setDisplayRatio(displayRatioH);
		
	}
	
	private void setDisplayRatio(float x)
	{
		int decimals = 2;
		displayRatio = Mathf.Round(x * Mathf.Pow(10, decimals))/Mathf.Pow(10, decimals);
	}
	
	private void buildTextureList()
	{
		selectedTextures.Clear();
		AssetDatabase.Refresh();
		foreach(string s in selectedFilename)
		{
			Texture2D t = (Texture2D)AssetDatabase.LoadAssetAtPath(projectRelatedPath+s, typeof(Texture2D));
			selectedTextures.Add(t);
		}
	}
	
	private string getCaptureName(string filename)
	{
		string[] names = filename.Split("_"[0]);
		return names[names.Length-1].Split("."[0])[0];
	}
}

#endif
