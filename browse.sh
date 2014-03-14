#! /bin/bash
limb=$(git branch | sed -n -e 's/^\* \(.*\)/\1/p')
url=$(git remote -v | grep push)
url=${url#*@}
url=${url%.git (*}
url=${url/:/\/}
cygstart "https://$url/tree/$limb"
