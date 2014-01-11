import sys
import re

class fileValidator:
	
	def __init__(self, filename):
		self.filename = filename
		self.replacements = []
		self.fullText = ""
		self.clean = True
		
	def less10(int):
		return ("0"+str(int))[-2:]
		
	def findInvalids(self):
		infile = open(self.filename,"r")
		lineNumber = 0
		
		while (True):
			lineNumber = lineNumber + 1
			row = infile.readline()
			if(row == ""):
				break

			self.fullText = self.fullText + row
			for i in range(0,len(row)):
				ch = ord(row[i])
				if((ch < 32 or ch > 126) and ch not in (10,13,9)):
					print("chr("+str(ch)+") on line "+str(lineNumber)+":"+str(i))
					self.replacements.append(row[i])
					self.clean=False
			
		infile.close()
	
	def fix(self):
		outfile = open(self.filename,"w")
		for rp in self.replacements:
			rx = re.compile(rp);
			self.fullText = rx.sub("&#"+less10(ord(rp))+";",self.fullText)
		
		outfile.writelines(self.fullText)

		outfile.close()

		
if __name__ == "__main__":
	fileToCheck = ""
	performFix = False
	
	if len(sys.argv) > 1:
		fileToCheck = sys.argv[1]
		if len(sys.argv) > 2:
			performFix = sys.argv[2]=="fix"
	else:
		print("File to check:")
		fileToCheck = sys.stdin.readline()
		
	fv = fileValidator(fileToCheck)
	fv.findInvalids()
	
	if performFix:
		fv.fix()

	if fv.clean:
		print "File is clean"
	
	print "Press [Enter] to quit"
	
	rl = sys.stdin.readline()
