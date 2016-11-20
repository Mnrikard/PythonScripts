set nocompatible

"VundleSection{{{
	filetype off
	" set the runtime path to include Vundle and initialize
	set rtp+=c:\users\mrikard\vimfiles\bundle\Vundle.vim
	call vundle#begin()

	" let Vundle manage Vundle, required
	Plugin 'VundleVim/Vundle.vim'

	"plugin on GitHub repo
	Plugin 'tpope/vim-fugitive'
	Plugin 'leafgarland/typescript-vim'
	Plugin 'vim-airline/vim-airline'
	Plugin 'PProvost/vim-ps1'
	Plugin 'tpope/vim-dispatch'
	Plugin 'tpope/vim-surround'
	Plugin 'scrooloose/syntastic'
	Plugin 'ervandew/supertab'
	Plugin 'OmniSharp/Omnisharp-vim'
	Plugin 'tpope/vim-vividchalk'
	Plugin 'Shougo/unite.vim'
	Plugin 'easymotion/vim-easymotion'
	Plugin 'adamclerk/vim-razor'

	call vundle#end()            " required
	filetype plugin indent on    " required
"}}}

"PluginSettings{{{
	let g:OmniSharp_server_type = 'roslyn'
	let g:OmniSharp_selector_ui = 'unite'
	let g:OmniSharp_host = "http://localhost:2000"
	"Set the type lookup function to use the preview window instead of the status line
	let g:OmniSharp_typeLookupInPreview = 0
	"Timeout in seconds to wait for a response from the server
	let g:OmniSharp_timeout = 1
	set noshowmatch

	let g:SuperTabDefaultCompletionType = 'context'
	let g:SuperTabContextDefaultCompletionType = "<c-x><c-o>"
	let g:SuperTabDefaultCompletionTypeDiscovery = ["&omnifunc:<c-x><c-o>","&completefunc:<c-x><c-n>"]
	let g:SuperTabClosePreviewOnPopupClose = 1
	set completeopt=longest,menuone,preview
	set splitbelow

	augroup omnisharp_commands
		autocmd!

		"Set autocomplete function to OmniSharp (if not using YouCompleteMe completion plugin)
		autocmd FileType cs,razor setlocal omnifunc=OmniSharp#Complete

		" Synchronous build (blocks Vim)
		"autocmd FileType cs nnoremap <F5> :wa!<cr>:OmniSharpBuild<cr>
		" Builds can also run asynchronously with vim-dispatch installed
		autocmd FileType cs nnoremap <leader>b :wa!<cr>:OmniSharpBuildAsync<cr>
		" automatic syntax check on events (TextChanged requires Vim 7.4)
		autocmd BufEnter,TextChanged,InsertLeave *.cs SyntasticCheck

		" Automatically add new cs files to the nearest project on save
		"autocmd BufWritePost *.cs call OmniSharp#AddToProject()

		"show type information automatically when the cursor stops moving
		autocmd CursorHold *.cs call OmniSharp#TypeLookupWithoutDocumentation()

		"The following commands are contextual, based on the current cursor position.

		autocmd FileType cs nnoremap gd :OmniSharpGotoDefinition<cr>
		autocmd FileType cs nnoremap <leader>fi :OmniSharpFindImplementations<cr>
		autocmd FileType cs nnoremap <leader>ft :OmniSharpFindType<cr>
		autocmd FileType cs nnoremap <leader>fs :OmniSharpFindSymbol<cr>
		autocmd FileType cs nnoremap <leader>fu :OmniSharpFindUsages<cr>
		"finds members in the current buffer
		autocmd FileType cs nnoremap <leader>fm :OmniSharpFindMembers<cr>
		" cursor can be anywhere on the line containing an issue
		autocmd FileType cs nnoremap <leader>x  :OmniSharpFixIssue<cr>
		autocmd FileType cs nnoremap <leader>fx :OmniSharpFixUsings<cr>
		autocmd FileType cs nnoremap <leader>tt :OmniSharpTypeLookup<cr>
		autocmd FileType cs nnoremap <leader>dc :OmniSharpDocumentation<cr>
		"navigate up by method/property/field
		autocmd FileType cs nnoremap <C-K> :OmniSharpNavigateUp<cr>
		"navigate down by method/property/field
		autocmd FileType cs nnoremap <C-J> :OmniSharpNavigateDown<cr>
	augroup end 
	set updatetime=500

	" Add syntax highlighting for types and interfaces
	nnoremap <leader>th :OmniSharpHighlightTypes<cr>

	let g:xml_syntax_folding=1
	au FileType xml,xsl,xslt,html,razor setlocal foldmethod=syntax
	set statusline+=%{fugitive#statusline()}
	"syntastic {{{
		set statusline+=%#warningmsg#
		set statusline+=%{SyntasticStatuslineFlag()}
		set statusline+=%*

		let g:syntastic_always_populate_loc_list = 1
		let g:syntastic_auto_loc_list = 2
		let g:syntastic_check_on_open = 1
		let g:syntastic_check_on_wq = 0
		let g:syntastic_cs_checkers = ["code_checker","issues","semantic","syntax"]
	"}}}
"}}}

"Sets{{{
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
	set list
	set listchars=tab:▶·,trail:·
	let mapleader=" "
	set encoding=utf8
	set laststatus=2
	colorscheme vividchalk
"}}}

"Maps{{{
	nnoremap <C-L> :nohl<CR><C-L>
	nnoremap <C-J> a<CR><Esc>k$
	nnoremap <C-O> O<Esc>
	nnoremap <C-o> o<Esc>
	vnoremap <C-c> "+y
	nnoremap <C-tab> :bn<Enter>
	nnoremap <C-S-tab> :bprev<Enter>
	nnoremap <C-s> :w<Enter>
	inoremap <C-s> <Esc>:w<Enter>i
	nnoremap <Leader>o i<Enter><Esc>
	nnoremap <C-s> :w<Enter>
	nnoremap <Leader>p "0p
	nnoremap <C-S-p> "+pl
	inoremap <C-S-p> <Esc>"+pli
	nnoremap <leader>ev :vsplit $MYVIMRC<cr>
	nnoremap <leader>sv :source $MYVIMRC<cr>
	inoremap jjk <Esc>
	nnoremap <leader>kd execute "normal! gg=G"
"}}}

"AutoCmd{{{
augroup myAutoCmds
	autocmd!
	autocmd FileType xml,xsl,xslt setlocal foldmethod=syntax
	autocmd BufEnter * syntax sync minlines=1000
	autocmd FileType vim setlocal foldmethod=marker
	autocmd FileType vim setlocal foldlevel=0
augroup end
"}}}

syntax on
syntax sync minlines=1000

"Commands {{{
	command! Nom execute "%s///g"
	command! BM execute "bufdo | bd!"

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
"}}}
