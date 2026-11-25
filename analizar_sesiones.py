"""Resume los registros de sesión del bar (sesion_*.csv) para la prueba en Cardboard.

Uso:
    python analizar_sesiones.py carpeta_con_csv/        (o una lista de archivos .csv)

Imprime una fila por sesión y los promedios. No necesita nada fuera de Python 3.
"""
import csv
import glob
import os
import re
import sys
from statistics import mean


def num(texto):
    """Lee números escritos con coma o con punto (el celular puede usar cualquiera)."""
    return float(texto.replace(",", "."))


def leer(ruta):
    filas = []
    with open(ruta, encoding="utf-8-sig") as f:
        for fila in csv.reader(f, delimiter=";"):
            if len(fila) < 3 or fila[0] == "tiempo_s":
                continue
            fila += [""] * (4 - len(fila))
            try:
                filas.append((num(fila[0]), fila[1], fila[2], fila[3]))
            except ValueError:
                pass
    return filas


def resumir(ruta):
    filas = leer(ruta)
    primero = lambda cat, ev=None: next((t for t, c, e, _ in filas if c == cat and (ev is None or e == ev)), None)
    contar = lambda cat, ev=None: sum(1 for _, c, e, _ in filas if c == cat and (ev is None or e == ev))
    servidos = []
    for _, c, e, d in filas:
        if c == "pedido" and e == "servido":
            m = re.search(r"en ([\d.,]+) s", d)
            if m:
                servidos.append(num(m.group(1)))
    sel = [d for _, c, _, d in filas if c == "seleccion"]
    return {
        "sesion": os.path.basename(ruta),
        "duracion_min": round(filas[-1][0] / 60, 1) if filas else 0,
        "primer_teletransporte_s": primero("teletransporte"),
        "primera_seleccion_s": primero("seleccion"),
        "pedidos_servidos": len(servidos),
        "seg_por_pedido": round(mean(servidos), 1) if servidos else None,
        "errores": contar("pedido", "error"),
        "teletransportes": contar("teletransporte"),
        "sel_mirada": sel.count("mirada"),
        "sel_toque": sel.count("toque"),
        "tareas_cierre": contar("cierre"),
        "llego_al_final": primero("fin", "tarjeta final") is not None,
        "habitue": next((e for _, c, e, _ in filas if c == "habitue"), "-"),
        "dardos": contar("dardos", "tiro"),
        "pool_tiros": contar("pool", "tiro"),
        "pool_embocadas": contar("pool", "embocada"),
        "arcade_revivio": primero("arcade", "revive") is not None,
    }


def main(args):
    rutas = []
    for a in args or ["."]:
        rutas += sorted(glob.glob(os.path.join(a, "sesion_*.csv"))) if os.path.isdir(a) else [a]
    if not rutas:
        print("No encontré archivos sesion_*.csv")
        return
    res = [resumir(r) for r in rutas]
    claves = list(res[0].keys())
    print(";".join(claves))
    for r in res:
        print(";".join("" if r[k] is None else str(r[k]) for k in claves))
    print()
    for k in claves[1:]:
        valores = [r[k] for r in res if isinstance(r[k], (int, float)) and not isinstance(r[k], bool) and r[k] is not None]
        si = [r[k] for r in res if isinstance(r[k], bool)]
        if valores:
            print(f"{k}: promedio {mean(valores):.1f} (mín {min(valores)}, máx {max(valores)})")
        elif si:
            print(f"{k}: {sum(si)} de {len(si)}")


if __name__ == "__main__":
    main(sys.argv[1:])
