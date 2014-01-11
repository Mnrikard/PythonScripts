import os
import sys

class compareDir:
	
	def __init__(self, basedir, lcase = True):
		if lcase:
			basedir = basedir.lower()
		self.directory = basedir
		self.folders = {}
		self.load(lcase)
		
	def load(self, lcase = True):
		for root,dirs,files in os.walk(self.directory):
			if lcase:
				root = root.lower()
			
			root = root.replace(self.directory,"")
			if len(root) > 0 and (root[0]=="/" or root[0] == "\\"):
				root = root[1:]
			
			self.folders[root] = files

def compareFileTexts(file1, file2):
	file = open(file1,"r")
	filec1 = file.read()
	file.close()

	file = open(file2,"r")
	filec2 = file.read()
	file.close()

	if filec1 != filec2:
		print(file1 + " !~ " + file2)

def compareFiles(dir1, files1, dir2, files2, compareText):
	for file in files1:
		if file in files2:
			if compareText:
				compareFileTexts(os.path.join(dir1,file), os.path.join(dir2,file))
		else:
			print("file: " + file + " exists in " + dir1 + " but not in " + dir2)
	
	for file in files2:
		if file not in files1:
			print("file: " + file + " exists in " + dir2 + " but not in " + dir1)
				

def compare2(dir1, dir2, compareText):
	comp1 = compareDir(dir1)
	comp2 = compareDir(dir2)

	for root,files in comp1.folders.items():
		if root in comp2.folders:
			compareFiles(os.path.join(dir1, root), comp1.folders[root], os.path.join(dir2,root), comp2.folders[root], compareText)
		else:
			print("folder: " + root + " exists in " + dir1 + " but not in " + dir2)
	
	for root,files in comp2.folders.items():
		if root not in comp1.folders:
			print("folder: " + root + " exists in " + dir2 + " but not in " + dir1)
	
if __name__ == "__main__":
	dir1 = None
	dir2 = None
	compareText = True

	if len(sys.argv) > 2:
		dir1 = sys.argv[1]
		dir2 = sys.argv[2]

		if len(sys.argv) > 3:
			compareText = sys.argv[3] == "1"

	else:
		print("First folder")
		dir1 = sys.stdin.readline()

		print("Compare against folder")
		dir2 = sys.stdin.readline()
	
	compare2(dir1, dir2, compareText)