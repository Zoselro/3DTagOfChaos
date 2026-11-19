import sys,os; sys.path.insert(0,os.getcwd())
import bpy; bpy.ops.wm.read_factory_settings(use_empty=True)
import lib
from lib import start, _parts
from objs_items import flour_sack
start(); flour_sack()
for o in _parts: print(o.name, len(o.data.polygons), tuple(round(d,2) for d in o.dimensions), o.location[:])
