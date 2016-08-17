
set nocompatible
filetype indent plugin on
syntax on
set whichwrap+=<,>,[,]
set hidden
set confirm
set wildmenu
set showcmd
set hlsearch
set ignorecase
set smartcase
set backspace=indent,eol,start
set autoindent
set nostartofline
set ruler
set laststatus=1
set confirm
set mouse=a
set cmdheight=2
set number
set notimeout ttimeout ttimeoutlen=200
set pastetoggle=<F11>
set shiftwidth=4
set tabstop=4
set softtabstop=4
set foldlevelstart=99
set foldmethod=indent
set nowrap
let mapleader=" "

syn sync minlines=1000

let g:closetag_html_style=1
au Filetype html,xml,xsl source $HOME/vimfiles/closetag.vim 

nnoremap <C-L> :nohl<CR><C-L>
nnoremap <C-J> a<CR><Esc>k$
nnoremap <C-o> o<Esc>
vnoremap <C-c> "+y
inoremap <C-p> <Esc>"+pli
nnoremap <C-p> "+p
nnoremap <C-tab> :bn<Enter>
nnoremap <C-S-tab> :bprev<Enter>
nnoremap <C-s> :w<Enter>
inoremap <C-s> <Esc>:w<Enter>i
nnoremap <Leader>o i<Enter><Esc>
nnoremap <C-s> :w<Enter>
nnoremap <C-S-a> ggVG
unmap <C-a>

let g:xml_syntax_folding=1
au FileType xml,xsl setlocal foldmethod=syntax
colorscheme mnr

command! Nom execute "%s///g"

function! MoveLine(lnum)
	let currline=line('.')
	echo currline "moving to" a:lnum
	execute "m" a:lnum
	execute currline
endfunction

function! MoveLineUp()
	let currline=line('.')
	execute "m" currline-2
endfunction

function! MoveLineDown()
	let currline=line('.')
	execute "m" currline+1
endfunction

command! -nargs=1 M execute "call MoveLine(<args>)"
command! -nargs=0 Md execute "call MoveLineDown()"
command! -nargs=0 Mu execute "call MoveLineUp()"

