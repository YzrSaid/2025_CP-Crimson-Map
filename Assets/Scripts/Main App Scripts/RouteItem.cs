using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;
using System.Collections.Generic;

public class RouteItem : MonoBehaviour
{
    [Header( "UI Elements" )]
    public TextMeshProUGUI titleText;
    public TextMeshProUGUI distanceText;
    public TextMeshProUGUI walkingTimeText;
    public TextMeshProUGUI viaModeText;
    public TextMeshProUGUI pathInfoText;
    public Button itemButton;

    [Header( "Path Toggle" )]
    public Button togglePathButton;
    public TextMeshProUGUI toggleButtonText;
    public GameObject openIcon;
    public GameObject closeIcon;

    [Header( "Visual Feedback" )]
    public Image backgroundImage;
    public Outline blackOutline;
    public Outline redOutline;
    public Color normalColor = new Color( 1f, 1f, 1f, 0.1f );
    public Color selectedColor = new Color( 0.2f, 0.8f, 0.3f, 0.3f );

    private int routeIndex;
    private System.Action<int> onRouteSelected;
    private bool isSelected = false;
    private bool isPathVisible = false;

    public void Initialize( int index, RouteData routeData, System.Action<int> selectCallback )
    {
        routeIndex = index;
        onRouteSelected = selectCallback;

        if ( titleText != null ) {
            if ( !string.IsNullOrEmpty( routeData.routeName ) ) {
                titleText.text = routeData.routeName;
            } else {
                titleText.text = $"Route #{index + 1}";
            }
        }

        if ( distanceText != null ) {
            distanceText.text = $"<b>Distance:</b> {routeData.formattedDistance}";
        }

        if ( walkingTimeText != null ) {
            walkingTimeText.text = $"<b>Time:</b> ~{routeData.walkingTime}";
        }

        if ( viaModeText != null ) {
            viaModeText.text = $"<b>Route:</b> {routeData.viaMode}";
        }

        if ( pathInfoText != null ) {
            string pathInfo = $"<b>Path ({routeData.path.Count} stops):</b>\n";
            for ( int i = 0; i < routeData.path.Count; i++ ) {
                var node = routeData.path[i].node;
                pathInfo += $"{i + 1}. {node.name}\n";
            }
            pathInfoText.text = pathInfo;
        }

        if ( itemButton != null ) {
            itemButton.onClick.RemoveAllListeners();
            itemButton.onClick.AddListener( OnItemClicked );
        }

        // Setup toggle button
        if ( togglePathButton != null ) {
            togglePathButton.onClick.RemoveAllListeners();
            togglePathButton.onClick.AddListener( TogglePathVisibility );
        }

        SetPathVisibility( false );

        SetSelected( false );

        if ( blackOutline != null ) {
            blackOutline.effectColor = HexToColor( "F3F3F3" );
            blackOutline.enabled = true;
        }

        if ( redOutline != null ) {
            redOutline.effectColor = HexToColor( "B81013" );
            redOutline.enabled = false;
        }
    }
    private Color HexToColor( string hex )
    {
        hex = hex.Replace( "#", "" );

        byte r = byte.Parse( hex.Substring( 0, 2 ), System.Globalization.NumberStyles.HexNumber );
        byte g = byte.Parse( hex.Substring( 2, 2 ), System.Globalization.NumberStyles.HexNumber );
        byte b = byte.Parse( hex.Substring( 4, 2 ), System.Globalization.NumberStyles.HexNumber );

        return new Color32( r, g, b, 255 );
    }

    private void OnItemClicked()
    {
        onRouteSelected?.Invoke( routeIndex );
    }

    private void TogglePathVisibility()
    {
        isPathVisible = !isPathVisible;
        SetPathVisibility( isPathVisible );
    }

    private void SetPathVisibility( bool visible )
    {
        isPathVisible = visible;

        // Show/hide the container
        if ( pathInfoText != null ) {
            pathInfoText.gameObject.SetActive( visible );
        }

        if ( toggleButtonText != null ) {
            toggleButtonText.text = visible ? "Hide Paths" : "Show Full Path";
        }

        if ( openIcon != null ) {
            openIcon.SetActive( !visible );
        }

        if ( closeIcon != null ) {
            closeIcon.SetActive( visible ); 
        }

        // Force layout rebuild
        StartCoroutine( RefreshLayoutNextFrame() );
    }

    private IEnumerator RefreshLayoutNextFrame()
    {
        yield return new WaitForEndOfFrame();

        LayoutRebuilder.ForceRebuildLayoutImmediate( GetComponent<RectTransform>() );

        if ( transform.parent != null ) {
            RectTransform parentRect = transform.parent.GetComponent<RectTransform>();
            if ( parentRect != null ) {
                LayoutRebuilder.ForceRebuildLayoutImmediate( parentRect );
            }
        }
    }

    public void SetSelected( bool selected )
    {
        isSelected = selected;

        if ( backgroundImage != null ) {
            backgroundImage.color = selected ? selectedColor : normalColor;
        }

        if ( blackOutline != null ) {
            blackOutline.enabled = !selected;  
        }

        if ( redOutline != null ) {
            redOutline.enabled = selected;  
        }
    }

    public bool IsSelected()
    {
        return isSelected;
    }

    void OnDestroy()
    {
        if ( itemButton != null ) {
            itemButton.onClick.RemoveAllListeners();
        }

        if ( togglePathButton != null ) {
            togglePathButton.onClick.RemoveAllListeners();
        }
    }
}