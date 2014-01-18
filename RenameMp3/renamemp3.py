"""Rename's mp3 files to artist - title.mp3"""

import fileinfo
import os
import re
import sys


def stripnulls(data):
    "strip whitespace and nulls"
    return data.replace("\00", " ").strip()


for root,folders,files in os.walk("c:\\msc"):
	for file in files:
		if re.search(".mp3$", file, 2) and not re.search(r"^[\w\d ]+ - [\w\d ]+\.mp3$", file, 2):
			info = fileinfo.MP3FileInfo(os.path.join(root,file))
			if "artist" in info and "title" in info:
				print("ren \"" + os.path.join(root,file) + "\" \"" + info["artist"]+" - "+info["title"]+".mp3\"")
				#break

			
			