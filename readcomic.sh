#! /bin/bash

function ReadXkcd {
	local html=$(curl "http://www.xkcd.com/")
	local img=$(echo $html | grep -Po '\<img src="http://imgs.xkcd.com/comics.+?\>')
	local comicText=$(echo $img | grep -Po '(?<=title=").+?(?=")')
	
	echo "${img}<ul><li>${comicText}</li></ul>";
}

function ReadQwantz {
	local html=$(curl "http://www.qwantz.com/index.php")
	local img=$(echo $html | grep -Po '\<img src="([^"]+?\.png)" class="comic" .+?\>')
	local text1=$(echo $img | grep -Po '(?<=title=").+?(?=")')
	local text2=$(echo $html | grep -Po '(?<=mailto:ryan@qwantz.com\?subject=).+?(?=")')
	local rss=$(curl "http://www.rsspect.com/rss/qwantz.xml")
	local titles=$(echo $rss | grep -Po '\<item\>\s+\<title\>.+?\</title\>')
	local text3="${titles%%</title*}"
    text3="${text3#*<title>}"
	
	echo "${img}<ul><li>${text1}</li><li>\r\n${text2}</li><li>${text3}</li></ul>"
}

function ReadPA {
    local html=$(curl "http://www.penny-arcade.com/comic/")
    local div=$(echo $html | grep -Po '\<div id="comicFrame".+?\</div\>')
    local img=$(echo $div | grep -m 1 -Po '\<img.+?\>')

    echo $img
}

echo "$(ReadQwantz)<hr/>$(ReadXkcd)<hr/>$(ReadPA)" | mail -s "Daily Comic" -a "Content-type:text/html;"  mnrikard@gmail.com
