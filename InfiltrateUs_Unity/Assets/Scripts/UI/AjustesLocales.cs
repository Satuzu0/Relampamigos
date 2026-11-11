using UnityEngine;

// Ajustes que viven solo en esta PC (PlayerPrefs). No tienen tabla en la BD porque no hace falta.
public static class AjustesLocales
{
    public static float Brillo { get { return PlayerPrefs.GetFloat("iu_brillo", 1f); } set { PlayerPrefs.SetFloat("iu_brillo", Mathf.Clamp01(value)); } }
    public static float VolGeneral { get { return PlayerPrefs.GetFloat("iu_vol_general", 0.8f); } set { PlayerPrefs.SetFloat("iu_vol_general", Mathf.Clamp01(value)); } }
    public static float VolMusica { get { return PlayerPrefs.GetFloat("iu_vol_musica", 0.7f); } set { PlayerPrefs.SetFloat("iu_vol_musica", Mathf.Clamp01(value)); } }
    public static float VolEfectos { get { return PlayerPrefs.GetFloat("iu_vol_efectos", 0.8f); } set { PlayerPrefs.SetFloat("iu_vol_efectos", Mathf.Clamp01(value)); } }
    public static bool PantallaCompleta { get { return PlayerPrefs.GetInt("iu_fullscreen", Screen.fullScreen ? 1 : 0) == 1; } set { PlayerPrefs.SetInt("iu_fullscreen", value ? 1 : 0); } }
    public static bool ContrasteAlto { get { return PlayerPrefs.GetInt("iu_contraste", 0) == 1; } set { PlayerPrefs.SetInt("iu_contraste", value ? 1 : 0); } }
    public static bool MostrarAyudas { get { return PlayerPrefs.GetInt("iu_ayudas", 1) == 1; } set { PlayerPrefs.SetInt("iu_ayudas", value ? 1 : 0); } }
    public static int ResolucionIndice { get { return PlayerPrefs.GetInt("iu_resolucion", -1); } set { PlayerPrefs.SetInt("iu_resolucion", value); } }

    public static void Restablecer()
    {
        PlayerPrefs.DeleteKey("iu_brillo");
        PlayerPrefs.DeleteKey("iu_vol_general");
        PlayerPrefs.DeleteKey("iu_vol_musica");
        PlayerPrefs.DeleteKey("iu_vol_efectos");
        PlayerPrefs.DeleteKey("iu_contraste");
        PlayerPrefs.DeleteKey("iu_ayudas");
        PlayerPrefs.DeleteKey("iu_fullscreen");
        PlayerPrefs.DeleteKey("iu_resolucion");
    }

    // Aplica lo que se puede aplicar ya mismo
    public static void Aplicar()
    {
        AudioListener.volume = VolGeneral;
        if (UIManager.Instancia != null) UIManager.Instancia.AplicarAjustesVisuales();
    }
}
